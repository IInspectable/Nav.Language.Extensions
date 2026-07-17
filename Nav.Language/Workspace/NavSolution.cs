#region Using Directives

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Modell einer entdeckten „Solution": die Menge aller <c>*.nav</c>-Dateien unterhalb einer Wurzel plus die
/// zum Auswerten nötige Provider-Kette (<see cref="ISyntaxProvider"/> → <see cref="ISemanticModelProvider"/>).
/// Grundlage solution-weiter Features (FindReferences, Call Hierarchy, Exit-Usages), die über
/// <see cref="ProcessCodeGenerationUnitsAsync"/> alle Dateien besuchen. Wird sowohl vom CLI-Codegenerator
/// (eigene Provider) als auch von der Workspace-Host-Schicht (<see cref="NavWorkspaceCore"/>, geteilte
/// overlay-fähige Provider) genutzt.
/// </summary>
public class NavSolution {

    /// <summary>
    /// Erzeugt eine Solution über <paramref name="solutionFiles"/>. Ohne explizite Provider wird ein
    /// <see cref="CachedSyntaxProvider"/> und ein darauf aufsetzender <see cref="SemanticModelProvider"/>
    /// verwendet; die Host-Schicht reicht hier ihre geteilten (overlay-fähigen) Provider herein, damit Scan
    /// und offene Dokumente denselben Cache benutzen.
    /// </summary>
    /// <param name="solutionRoot">Wurzelverzeichnis der Solution (oder <c>null</c> für die leere Solution).</param>
    /// <param name="solutionFiles">Die entdeckten <c>*.nav</c>-Dateien.</param>
    /// <param name="syntaxProvider">Optionaler Syntax-Provider; Standard: <see cref="CachedSyntaxProvider"/>.</param>
    /// <param name="semanticModelProvider">Optionaler Semantik-Provider; Standard: <see cref="SemanticModelProvider"/> über <paramref name="syntaxProvider"/>.</param>
    public NavSolution(DirectoryInfo? solutionRoot,
                       ImmutableArray<FileInfo> solutionFiles,
                       ISyntaxProvider? syntaxProvider = null,
                       ISemanticModelProvider? semanticModelProvider = null) {

        SolutionDirectory = solutionRoot;
        SolutionFiles     = solutionFiles;

        SyntaxProvider        = syntaxProvider        ?? new CachedSyntaxProvider();
        SemanticModelProvider = semanticModelProvider ?? new SemanticModelProvider(SyntaxProvider);
    }

    /// <summary>Der Syntax-Provider dieser Solution (Lexer/Parser → <see cref="CodeGenerationUnitSyntax"/>).</summary>
    public ISyntaxProvider        SyntaxProvider        { get; }
    /// <summary>Der Semantik-Provider dieser Solution (Syntaxbaum → <see cref="CodeGenerationUnit"/>).</summary>
    public ISemanticModelProvider SemanticModelProvider { get; }

    /// <summary>Wurzelverzeichnis der Solution, oder <c>null</c> für die leere Solution.</summary>
    public DirectoryInfo? SolutionDirectory { get; }

    /// <summary>Die entdeckten <c>*.nav</c>-Dateien der Solution.</summary>
    public ImmutableArray<FileInfo> SolutionFiles { get; }

    /// <summary>Die leere Solution (keine Wurzel, keine Dateien) — neutraler Ausgangs-/Fehlzustand.</summary>
    public static NavSolution Empty = new(null, ImmutableArray<FileInfo>.Empty);

    /// <summary>Die Nav-Dateiendung <c>.nav</c>.</summary>
    public const string FileExtension = ".nav";

    /// <summary>Das Suchmuster <c>*.nav</c> für <see cref="Directory.EnumerateFiles(string,string)"/> (grob; siehe <see cref="HasNavExtension"/>).</summary>
    public static string SearchFilter => "*" + FileExtension;

    /// <summary>
    /// Prüft die EXAKTE Dateiendung <c>.nav</c>. Nötig, weil Windows-<see cref="Directory.EnumerateFiles(string,string)"/>
    /// bei einem 3-Zeichen-Suchmuster wie <c>*.nav</c> aus Alt-8.3-Gründen AUCH Dateien mit längerer, gleich
    /// beginnender Endung liefert (z.B. <c>.navignore</c>) — die dürfen nicht als Nav-Datei behandelt werden.
    /// </summary>
    public static bool HasNavExtension(string path) =>
        string.Equals(Path.GetExtension(path), FileExtension, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Entdeckt rekursiv alle <c>*.nav</c>-Dateien unterhalb von <paramref name="directory"/> und baut daraus
    /// eine Solution. Das Windows-Suchmuster <see cref="SearchFilter"/> matcht auch länger endende Dateien
    /// (z.B. <c>.navignore</c>); diese werden per <see cref="HasNavExtension"/> exakt herausgefiltert. Ohne
    /// gültiges Verzeichnis oder bei Abbruch wird <see cref="Empty"/> geliefert.
    /// </summary>
    public static Task<NavSolution> FromDirectoryAsync(DirectoryInfo? directory, CancellationToken cancellationToken) {

        // netstandard2.0: String.IsNullOrEmpty verengt nicht, und directory?.FullName lässt directory
        // nullable — daher directory explizit prüfen, bevor FullName dereferenziert wird.
        if (directory == null || String.IsNullOrEmpty(directory.FullName)) {
            return Task.FromResult(Empty);
        }

        var itemBuilder = ImmutableArray.CreateBuilder<FileInfo>();

        foreach (var file in Directory.EnumerateFiles(directory.FullName,
                                                      SearchFilter,
                                                      SearchOption.AllDirectories)) {

            if (cancellationToken.IsCancellationRequested) {
                return Task.FromResult(Empty);
            }

            // Windows *.nav matcht auch .navignore & Co. (3-Zeichen-Endung) — exakt filtern.
            if (!HasNavExtension(file)) {
                continue;
            }

            var fileInfo = new FileInfo(file);
            itemBuilder.Add(fileInfo);

        }

        var solution = new NavSolution(directory, itemBuilder.ToImmutableArray());

        return Task.FromResult(solution);
    }

    /// <summary>
    /// Ruft <paramref name="asyncAction"/> für jede <see cref="CodeGenerationUnit"/> der Solution auf —
    /// Grundoperation solution-weiter Features (FindReferences, Call Hierarchy, Exit-Usages).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Die Besuchsreihenfolge ist auf frühe Treffer optimiert: Ist ein <paramref name="startingUnit"/> gegeben
    /// (die Datei mit der gesuchten Definition), wird zuerst sie selbst, dann die übrigen Dateien ihres
    /// Verzeichnisses und erst zuletzt die restliche Solution besucht. Bereits besuchte Dateien werden
    /// case-insensitiv dedupliziert (Windows-Pfade), damit dieselbe Datei nicht doppelt verarbeitet wird.
    /// </para>
    /// <para>
    /// Der teure Semantikmodell-Aufbau der Restmenge läuft parallel (der Bau-Pfad ist nebenläufigkeitssicher);
    /// <paramref name="asyncAction"/> selbst wird dagegen weiterhin sequenziell und in stabiler Datei-Reihenfolge
    /// (<c>AsOrdered</c>) auf dem Aufrufer-Fluss aufgerufen — die Callbacks brauchen daher nicht thread-sicher zu sein.
    /// </para>
    /// </remarks>
    /// <param name="asyncAction">Pro besuchter Unit aufgerufene Aktion.</param>
    /// <param name="startingUnit">Optionale Einstiegs-Unit für die Nah-zuerst-Reihenfolge; <c>null</c> = reine Solution-Reihenfolge.</param>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    public async Task ProcessCodeGenerationUnitsAsync(Func<CodeGenerationUnit, Task> asyncAction,
                                                      CodeGenerationUnit? startingUnit,
                                                      CancellationToken cancellationToken) {

        // Datei-Dedup case-insensitiv: Windows-Pfade sind case-insensitiv, und die Pfad-Quellen dieses
        // Scans können in der Schreibweise abweichen (z.B. der normalisierte — kleingeschriebene —
        // startingUnit-Pfad eines Hosts vs. die Original-Schreibweise der SolutionFiles). Ein
        // case-sensitives Set würde dieselbe Datei dann doppelt verarbeiten (Referenzen/Aufrufe doppelt).
        var seenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Falls uns eine CGU für den Einstieg gegeben wurde, beginne wir die Suche hier,
        // bevor alle anderen CGUs der Solution durchkaufen werden.            
        if (startingUnit != null) {

            var navFile = startingUnit.Syntax.SyntaxTree.SourceText.FileInfo;
            var navDir  = navFile?.Directory;

            // 1. In dem File anfangen, in dem sich auch die Definition befindet, deren Referenzen gesucht werden
            await asyncAction(startingUnit);

            if (navFile != null) {
                // Wenn das Definitionsfile einen Dateinamen hat, dann zu den bereits gesehenen hinzufügen.
                seenFiles.Add(navFile.FullName);
            }

            // 2. Wir suchen in dem Verzeichnis, in dem sich auch das Nav File der Definition befindet. Die Wahscheinlichkeit ist recht groß,
            //    dass hier bereits erste Treffer ermittelt werden.
            if (navDir != null) {

                foreach (var fileName in Directory.EnumerateFiles(navDir.FullName, SearchFilter)) {

                    cancellationToken.ThrowIfCancellationRequested();

                    // s. HasNavExtension: *.nav matcht unter Windows auch .navignore & Co.
                    if (!HasNavExtension(fileName)) {
                        continue;
                    }

                    await ProcessFile(fileName);
                }

            }
        }

        // 3. Zu guter Letzt durchsuchen wir alle übrigen Files der "Solution", was mittlerweile ~1400 Dateien sind, und
        //    entsprechend lange dauert.

        // Dedup VOR der Parallel-Stufe: seenFiles ist nicht thread-sicher und wird ab hier nicht mehr verändert.
        var remainingFiles = new List<string>();
        foreach (var file in SolutionFiles) {
            if (seenFiles.Add(file.FullName)) {
                remainingFiles.Add(file.FullName);
            }
        }

        // Der Semantikmodell-Aufbau ist der teure, CPU-gebundene Teil des Scans und läuft parallel — der
        // Bau-Pfad ist nebenläufigkeitssicher (Provider-Caches sind ConcurrentDictionary bzw.
        // ConditionalWeakTable, die gebauten Units unveränderlich). asyncAction dagegen läuft weiterhin
        // sequenziell und in Datei-Reihenfolge (AsOrdered) auf dem Aufrufer-Fluss — Aufrufer brauchen
        // nach wie vor keine thread-sicheren Callbacks.
        var codeGenerationUnits = remainingFiles
                                  .AsParallel()
                                  .AsOrdered()
                                  .WithCancellation(cancellationToken)
                                  .Select(fileName => SemanticModelProvider.GetSemanticModel(fileName, cancellationToken));

        foreach (var codeGen in codeGenerationUnits) {

            cancellationToken.ThrowIfCancellationRequested();

            if (codeGen == null) {
                continue;
            }

            await asyncAction(codeGen);
        }

        async Task ProcessFile(string fileName) {

            if (!seenFiles.Add(fileName)) {
                return;
            }

            var codeGen = SemanticModelProvider.GetSemanticModel(fileName, cancellationToken);
            if (codeGen == null) {
                return;
            }

            await asyncAction(codeGen);
        }
    }

}