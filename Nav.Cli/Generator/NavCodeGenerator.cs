#region Using Directives

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

using Pharmatechnik.Nav.Utilities.IO;
using Pharmatechnik.Nav.Language.CodeGen;
using Pharmatechnik.Nav.Language.Logging;
using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.Generator;

/// <summary>
/// Der Standardpfad des CLI-Hosts (<c>nav.exe</c>): der Codegenerator. Sammelt die <c>.nav</c>-Eingaben
/// ein, verdrahtet daraus die Engine-<see cref="NavCodeGeneratorPipeline"/> und lässt sie laufen; bei
/// Erfolg schreibt er zusätzlich die Manifeste für inkrementelle MSBuild-Builds. Die eigentliche
/// Codeerzeugung liegt vollständig in der Engine — diese Klasse steuert nur Datei-Discovery,
/// Options-Übersetzung und Manifest-Ausgabe bei.
/// </summary>
class NavCodeGenerator {

    /// <summary>
    /// Führt den Codegenerator-Lauf für die übergebene Kommandozeile aus: Eingaben einsammeln
    /// (<see cref="CollectFiles"/>), Pipeline bauen (<see cref="CreatePipeline"/>), laufen lassen und —
    /// nur bei Erfolg — die Outputs- und Abhängigkeits-Manifeste schreiben (<see cref="WriteManifest"/>).
    /// </summary>
    /// <param name="cl">Das geparste Options-Modell; steuert Eingaben, Erzeugungsumfang, Logging und die
    /// Manifest-Pfade (<see cref="CommandLine.ManifestFile"/>, <see cref="CommandLine.DependencyManifestFile"/>).</param>
    /// <returns><c>0</c> bei Erfolg, <c>1</c> bei einem regulären Fehlschlag der Pipeline
    /// (<see cref="NavCodeGeneratorPipeline.RunResult.Succeeded"/> ist <c>false</c>), <c>-1</c> bei einer
    /// unbehandelten <see cref="Exception"/> — diese Codes werden zum Prozess-Exit-Code des Hosts.</returns>
    public int Run(CommandLine cl) {

        var logger = new ConsoleLogger(
            fullPaths : cl.FullPaths,
            noWarnings: cl.NoWarnings,
            verbose   : cl.Verbose);

        try {

            var fileSpecs = CollectFiles(cl, logger);
            var pipeline  = CreatePipeline(cl, logger);

            var result = pipeline.Run(fileSpecs);

            // Bei gesetztem Manifest-Pfad alle erzeugten Ausgabedateien (auch inhaltsgleich
            // übersprungene) für den inkrementellen MSBuild-Build protokollieren. Nur bei Erfolg —
            // sonst bliebe das (alte) Manifest älter als die Inputs und der nächste Build generiert
            // erneut.
            if (result.Succeeded && !cl.ManifestFile.IsNullOrEmpty()) {
                WriteManifest(cl.ManifestFile, result.GeneratedFiles.Select(r => r.FileName));
            }

            // Abhängigkeits-Manifest: die per taskref eingelesenen Dateien, die selbst keine
            // Eingabedateien sind. Der inkrementelle Build liest dieses Manifest beim nächsten Lauf und
            // hängt die Pfade als zusätzliche Inputs an — so löst eine Änderung an einer solchen
            // Abhängigkeit korrekt einen Regen aus, statt fälschlich übersprungen zu werden. Nur bei
            // Erfolg schreiben (analog zum Outputs-Manifest).
            if (result.Succeeded && !cl.DependencyManifestFile.IsNullOrEmpty()) {
                WriteManifest(cl.DependencyManifestFile, result.IncludedFiles);
            }

            return result.Succeeded ? 0 : 1;

        } catch (Exception ex) {

            logger.LogError(ex.ToString());

            return -1;
        }
    }

    /// <summary>
    /// Schreibt eine Dateiliste als Manifest: die Pfade werden zu absoluten Pfaden aufgelöst, leere
    /// verworfen, dann case-insensitiv dedupliziert und sortiert. Ein fehlendes Zielverzeichnis wird
    /// angelegt. Dient dem inkrementellen MSBuild-Build als Inputs-/Outputs-Nachweis.
    /// </summary>
    /// <param name="manifestFile">Der Zielpfad des Manifests (vgl. <see cref="CommandLine.ManifestFile"/>
    /// bzw. <see cref="CommandLine.DependencyManifestFile"/>).</param>
    /// <param name="generatedFiles">Die zu protokollierenden Datei-Pfade (erzeugte Ausgaben bzw. per
    /// <c>taskref</c> eingelesene Abhängigkeiten).</param>
    static void WriteManifest(string manifestFile, IEnumerable<string> generatedFiles) {

        var lines = generatedFiles
                   .Select(PathHelper.GetFullPathNoThrow)
                   .Where(path => !path.IsNullOrEmpty())
                   .Distinct(StringComparer.OrdinalIgnoreCase)
                   .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                   .ToList();

        var directory = Path.GetDirectoryName(manifestFile);
        if (!directory.IsNullOrEmpty()) {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllLines(manifestFile, lines);
    }

    /// <summary>
    /// Baut die Engine-<see cref="NavCodeGeneratorPipeline"/> aus der Kommandozeile: übersetzt das
    /// Host-Options-Modell in die Engine-<see cref="GenerationOptions"/> (inkl. der Aufspaltung von
    /// <see cref="CommandLine.GenerationOptions"/> in die einzelnen <c>Generate*</c>-Flags), wählt je
    /// nach <see cref="CommandLine.UseSyntaxCache"/> die passende <see cref="ISyntaxProviderFactory"/>
    /// und validiert die Wurzelverzeichnis-Optionen, bevor die Pipeline erzeugt wird.
    /// </summary>
    /// <param name="cl">Das geparste Options-Modell.</param>
    /// <param name="logger">Der an die Pipeline weitergereichte <see cref="ConsoleLogger"/>.</param>
    /// <returns>Die fertig konfigurierte Pipeline.</returns>
    /// <exception cref="ArgumentException">Eine der Wurzelverzeichnis-Optionen ist ungültig
    /// (siehe <c>ValidateOptions</c>).</exception>
    static NavCodeGeneratorPipeline CreatePipeline(CommandLine cl, ConsoleLogger logger) {

        var syntaxProviderFactory = cl.UseSyntaxCache ? SyntaxProviderFactory.Cached : SyntaxProviderFactory.Default;

        var options = new GenerationOptions {
            Force                = cl.Force,
            Strict               = cl.Strict,
            GenerateToClasses    = (cl.GenerationOptions & CodeGenerationOptions.ToClasses)   != 0,
            GenerateWflClasses   = (cl.GenerationOptions & CodeGenerationOptions.WflClasses)  != 0,
            GenerateIwflClasses  = (cl.GenerationOptions & CodeGenerationOptions.IwflClasses) != 0,
            NullableContext      = cl.NullableContext,
            ProjectRootDirectory = cl.ProjectRootDirectory,
            IwflRootDirectory    = cl.IwflRootDirectory,
            WflRootDirectory     = cl.WflRootDirectory,

        };

        ValidateOptions();

        var pipeline = NavCodeGeneratorPipeline.Create(options: options, logger: logger, syntaxProviderFactory: syntaxProviderFactory);

        return pipeline;

        void ValidateOptions() {

            if (!options.ProjectRootDirectory.IsNullOrEmpty() &&
                !Directory.Exists(options.ProjectRootDirectory)) {
                throw new ArgumentException($"Das Project Wurzelverzeichnis '{options.ProjectRootDirectory}' exisitiert nicht.");
            }

            if (!options.WflRootDirectory.IsNullOrEmpty() &&
                options.ProjectRootDirectory.IsNullOrEmpty()
               ) {
                throw new ArgumentException($"es wurde ein alternatives WFL Wurzelverzeichnis '{options.IwflRootDirectory}' angegeben, aber kein Project Wurzelverzeichnis.");
            }

            if (!options.IwflRootDirectory.IsNullOrEmpty() &&
                options.ProjectRootDirectory.IsNullOrEmpty()
               ) {
                throw new ArgumentException($"es wurde ein alternatives IWFL Wurzelverzeichnis '{options.IwflRootDirectory}' angegeben, aber kein Project Wurzelverzeichnis.");
            }
        }
    }

    /// <summary>
    /// Ermittelt die <c>.nav</c>-Eingaben als <see cref="FileSpec"/>-Menge aus den beiden sich ergänzenden
    /// Quellen: dem <c>/d</c>-Verzeichnismodus (rekursiver Scan von <see cref="CommandLine.Directory"/>,
    /// dessen Verzeichnis zugleich die <c>.navignore</c>-Scangrenze ist) und dem <c>/s</c>-Einzeldateimodus
    /// (<see cref="CommandLine.Sources"/>, für den je Datei die <c>.navignore</c>-Vorfahren ausgewertet
    /// werden). In beiden Fällen wird per <see cref="NavSolution.HasNavExtension"/> exakt auf die
    /// <c>.nav</c>-Endung gefiltert, weil <c>*.nav</c> unter Windows auch <c>.navignore</c> &amp; Co.
    /// matcht; per <see cref="NavIgnore"/> ausgeschlossene Dateien werden übersprungen und protokolliert.
    /// </summary>
    /// <param name="cl">Das geparste Options-Modell (liefert Verzeichnis bzw. Einzeldateien).</param>
    /// <param name="logger">Zum Protokollieren übersprungener Dateien.</param>
    /// <returns>Die einzulesenden Eingabedateien.</returns>
    static IEnumerable<FileSpec> CollectFiles(CommandLine cl, ConsoleLogger logger) {

        var result = new List<FileSpec>();

        // /d:-Modus: Das Verzeichnis ist zugleich die .navignore-Scangrenze.
        if (cl.Directory != null) {

            var ignore = NavIgnore.Load(cl.Directory);

            foreach (var file in Directory.EnumerateFiles(cl.Directory, NavSolution.SearchFilter, SearchOption.AllDirectories)) {

                // Windows *.nav matcht auch .navignore & Co. (3-Zeichen-Endung) — exakt filtern.
                if (!NavSolution.HasNavExtension(file)) {
                    continue;
                }

                if (ignore.IsIgnored(file)) {
                    logger.LogVerbose($"Übersprungen (.navignore): {file}");
                    continue;
                }

                result.Add(new FileSpec(identity: PathHelper.GetRelativePath(cl.Directory, file), fileName: file));
            }
        }

        // /s:-Modus: Einzeldateien ohne gemeinsame Wurzel — pro Datei die .navignore-Vorfahren auswerten.
        if (cl.Sources != null) {

            var ignoreByDir = new Dictionary<string, NavIgnore>(StringComparer.OrdinalIgnoreCase);

            foreach (var source in cl.Sources) {

                var fullPath = PathHelper.GetFullPathNoThrow(source);
                var dir      = Path.GetDirectoryName(fullPath) ?? fullPath;

                if (!ignoreByDir.TryGetValue(dir, out var ignore)) {
                    ignore           = NavIgnore.LoadForAncestors(dir);
                    ignoreByDir[dir] = ignore;
                }

                if (ignore.IsIgnored(fullPath)) {
                    logger.LogInfo($"Übersprungen (.navignore): {source}");
                    continue;
                }

                result.Add(FileSpec.FromFile(source));
            }
        }

        return result;
    }

}