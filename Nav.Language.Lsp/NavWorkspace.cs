#region Using Directives

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Protocol = Microsoft.VisualStudio.LanguageServer.Protocol;

#endregion

namespace Pharmatechnik.Nav.Language.Lsp;

/// <summary>
/// LSP-Schale über dem gemeinsamen <see cref="NavWorkspaceCore"/> (Solution + Overlay + Diagnostics). Ergänzt
/// die LSP-spezifischen Belange, die der Kern bewusst NICHT kennt: das Push-Modell (<c>publishDiagnostics</c>),
/// die dependency-aware Re-Diagnose über den <see cref="IncludeDependencyGraph"/> und die <c>.navignore</c>-Stummschaltung.
/// </summary>
class NavWorkspace {

    readonly NavWorkspaceCore _core = new();

    // Include-Abhängigkeiten (Inkludierer → inkludierte Dateien) für die dependency-aware Re-Diagnose.
    // Beim Solution-Scan (PublishAllDiagnosticsAsync) komplett befüllt und bei jeder Unit-Berechnung
    // aufgefrischt, sodass ein didChange auch die (transitiv) inkludierenden Dateien neu diagnostizieren kann.
    readonly IncludeDependencyGraph _dependencies = new();

    // Hierarchische .navignore-Treffer: ignorierte Dateien bleiben in der Solution (weiterhin als include-Ziel
    // auflösbar und navigierbar), publizieren aber keine Diagnostics (leeres Array → löscht ggf. Angezeigtes).
    NavIgnore _ignore = NavIgnore.Empty;

    public int FileCount => _core.FileCount;

    /// <summary>Die geladene Solution (alle *.nav) — Grundlage für solution-weite Referenzsuche.</summary>
    public NavSolution Solution => _core.Solution;

    public async Task LoadAsync(string? rootPath, CancellationToken cancellationToken) {

        await _core.LoadAsync(rootPath, cancellationToken);

        _ignore = _core.SolutionDirectory == null
            ? NavIgnore.Empty
            : NavIgnore.Load(_core.SolutionDirectory.FullName);
    }

    /// <summary>Ist die Datei durch eine <c>.navignore</c>-Regel von Diagnostics ausgenommen?</summary>
    public bool IsIgnored(string filePath) => _ignore.IsIgnored(filePath);

    /// <summary>
    /// Lädt die <c>.navignore</c>-Regeln neu (nach einer Änderung an einer <c>.navignore</c>-Datei). Die
    /// Solution-Dateiliste bleibt unberührt; nur das Ignore-Urteil ändert sich.
    /// </summary>
    public void ReloadIgnore() {
        _ignore = _core.SolutionDirectory == null
            ? NavIgnore.Empty
            : NavIgnore.Load(_core.SolutionDirectory.FullName);
    }

    /// <summary>Öffnet/aktualisiert ein Dokument im Overlay (Schlüssel = normalisierter Pfad).</summary>
    public void OpenOrUpdate(string normalizedPath, string text) => _core.OpenOrUpdate(normalizedPath, text);

    /// <summary>Schließt ein Dokument — die Wahrheit liegt wieder auf Platte.</summary>
    public void Close(string normalizedPath) => _core.Close(normalizedPath);

    /// <summary>Ist das Dokument aktuell offen (Overlay vorhanden)? Dann schlägt das Overlay die Platte.</summary>
    public bool IsOpen(string normalizedPath) => _core.IsOpen(normalizedPath);

    /// <summary>
    /// Invalidiert den Platten-Syntax-Cache einer extern geänderten Datei (das Overlay bleibt unberührt) —
    /// der nächste Zugriff liest sie frisch von Platte.
    /// </summary>
    public void InvalidateDiskCache(string normalizedPath) => _core.InvalidateCache(normalizedPath);

    /// <summary>
    /// Nimmt eine neu angelegte Datei in die Solution auf (damit sie an solution-weiten Features teilnimmt).
    /// </summary>
    public void AddSolutionFile(string filePath) => _core.AddSolutionFile(filePath);

    /// <summary>
    /// Entfernt eine gelöschte Datei aus der Solution und aus dem Abhängigkeitsgraphen. Kanten ANDERER Dateien,
    /// die sie inkludierten, bleiben erhalten (werden beim Neudiagnostizieren der Inkludierer aufgefrischt).
    /// </summary>
    public void RemoveSolutionFile(string normalizedPath) {
        _dependencies.Remove(normalizedPath);
        _core.RemoveSolutionFile(normalizedPath);
    }

    /// <summary>Syntaxbaum eines Dokuments (overlay-bewusst) — Grundlage für Semantic Tokens.</summary>
    public SyntaxTree? GetSyntaxTree(string filePath, CancellationToken cancellationToken) {
        return _core.GetSyntaxTree(filePath, cancellationToken);
    }

    /// <summary>Semantisches Modell eines Dokuments (overlay-bewusst) — Grundlage für Document Symbols.</summary>
    public CodeGenerationUnit? GetCodeGenerationUnit(string filePath, CancellationToken cancellationToken) {
        return _core.GetCodeGenerationUnit(filePath, cancellationToken);
    }

    /// <summary>Diagnostics für ein einzelnes Dokument (overlay-bewusst, mit <c>.navignore</c>-Stummschaltung).</summary>
    public IReadOnlyList<Diagnostic> GetDiagnostics(string filePath, CancellationToken cancellationToken) {

        var unit = _core.GetCodeGenerationUnit(filePath, cancellationToken);
        if (unit == null) {
            return Array.Empty<Diagnostic>();
        }

        // Beim Diagnostizieren zugleich die Include-Kanten der Datei auffrischen (frei, da die Unit ohnehin
        // gebaut wurde) — so bleibt der Graph für jede berührte Datei aktuell. Bewusst VOR dem Ignore-Gate,
        // damit ignorierte Dateien weiter als include-Ziel im Graphen stehen.
        RecordIncludes(unit);

        // .navignore-Treffer: keine Diagnostics (Datei bleibt geladen/auflösbar).
        if (_ignore.IsIgnored(filePath)) {
            return Array.Empty<Diagnostic>();
        }

        return DiagnosticsComputer.FromUnit(unit, filePath);
    }

    /// <summary>
    /// Aktualisiert die Include-Vorwärtskanten einer Datei aus ihrem semantischen Modell
    /// (<see cref="CodeGenerationUnit.Includes"/> liefert die aufgelösten Pfade der inkludierten Dateien).
    /// </summary>
    void RecordIncludes(CodeGenerationUnit unit) {

        var fullPath = unit.Syntax.SyntaxTree.SourceText.FileInfo?.FullName;
        if (string.IsNullOrEmpty(fullPath)) {
            return;
        }

        _dependencies.SetIncludes(fullPath, unit.Includes.Select(include => include.FileName));
    }

    /// <summary>
    /// Publiziert die Diagnostics für alle Dateien, die <paramref name="changedFilePath"/> (transitiv)
    /// inkludieren. Nötig, weil sich deren Cross-File-Diagnostics ändern können, ohne dass sie selbst editiert
    /// wurden. Die Publish-URI wird — wie beim Solution-Scan — aus der <c>FileInfo.FullName</c> der Unit gebildet,
    /// damit sie sich nicht durch abweichende Laufwerks-/Pfad-Schreibweisen von der initialen Form unterscheidet
    /// (sonst blieben Geister-Diagnostics am Client stehen).
    /// </summary>
    public async Task PublishDependentsAsync(string changedFilePath,
                                             Func<Uri, Protocol.Diagnostic[], Task> publishAsync,
                                             CancellationToken cancellationToken) {

        foreach (var dependentKey in _dependencies.GetDependentsClosure(changedFilePath)) {

            cancellationToken.ThrowIfCancellationRequested();

            var unit = _core.GetCodeGenerationUnit(dependentKey, cancellationToken);
            if (unit == null) {
                continue;
            }

            var fullPath = unit.Syntax.SyntaxTree.SourceText.FileInfo?.FullName;
            if (string.IsNullOrEmpty(fullPath)) {
                continue;
            }

            // Kanten der inkludierenden Datei gleich mit auffrischen.
            RecordIncludes(unit);

            // Ist der Inkludierer selbst ignoriert, ein leeres Array publizieren (statt zu überspringen),
            // damit zuvor angezeigte Diagnostics gelöscht werden.
            var lspDiagnostics = _ignore.IsIgnored(fullPath)
                ? Array.Empty<Protocol.Diagnostic>()
                : DiagnosticsComputer.FromUnit(unit, fullPath)
                                     .Select(LspMapper.ToLsp)
                                     .ToArray();

            await publishAsync(new Uri(fullPath), lspDiagnostics);
        }
    }

    /// <summary>Publiziert Diagnostics für sämtliche Workspace-Dateien.</summary>
    public Task PublishAllDiagnosticsAsync(Func<Uri, Protocol.Diagnostic[], Task> publishAsync, CancellationToken cancellationToken) {

        return _core.Solution.ProcessCodeGenerationUnitsAsync(
            asyncAction: async unit => {

                var fullPath = unit.Syntax.SyntaxTree.SourceText.FileInfo?.FullName;
                if (string.IsNullOrEmpty(fullPath)) {
                    return;
                }

                // Include-Kanten beim Scan vollständig befüllen — auch für nie geöffnete Inkludierer, damit
                // ein späteres didChange deren Re-Diagnose anstoßen kann.
                RecordIncludes(unit);

                // .navignore-Treffer: leeres Array publizieren (Datei bleibt für include/Navigation geladen).
                var lspDiagnostics = _ignore.IsIgnored(fullPath)
                    ? Array.Empty<Protocol.Diagnostic>()
                    : DiagnosticsComputer.FromUnit(unit, fullPath).Select(LspMapper.ToLsp).ToArray();

                await publishAsync(new Uri(fullPath), lspDiagnostics);
            },
            startingUnit: null,
            cancellationToken: cancellationToken);
    }
}
