#region Using Directives

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Utilities.IO;

using Lsp = Microsoft.VisualStudio.LanguageServer.Protocol;

#endregion

namespace Pharmatechnik.Nav.Language.Server;

/// <summary>
/// Hält den Nav-Workspace (alle *.nav unterhalb der rootUri) samt Overlay für offene Dokumente.
/// Ein einziger <see cref="OverlaySyntaxProvider"/> versorgt sowohl den Workspace-Scan als auch die
/// Diagnostics offener Dokumente — dadurch wirken ungespeicherte Edits korrekt auch auf
/// Cross-File-Diagnostics ("offenes Dokument schlägt Platte").
/// </summary>
class NavWorkspace {

    readonly OverlaySyntaxProvider  _syntaxProvider;
    readonly ISemanticModelProvider _semanticModelProvider;

    // Include-Abhängigkeiten (Inkludierer → inkludierte Dateien) für die dependency-aware Re-Diagnose.
    // Beim Solution-Scan (PublishAllDiagnosticsAsync) komplett befüllt und bei jeder Unit-Berechnung
    // aufgefrischt, sodass ein didChange auch die (transitiv) inkludierenden Dateien neu diagnostizieren kann.
    readonly IncludeDependencyGraph _dependencies = new();

    NavSolution _solution = NavSolution.Empty;

    public NavWorkspace() {
        _syntaxProvider        = new OverlaySyntaxProvider();
        _semanticModelProvider = new SemanticModelProvider(_syntaxProvider);
    }

    public int FileCount => _solution.SolutionFiles.Length;

    /// <summary>Die geladene Solution (alle *.nav) — Grundlage für solution-weite Referenzsuche.</summary>
    public NavSolution Solution => _solution;

    public async Task LoadAsync(string? rootPath, CancellationToken cancellationToken) {

        if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath)) {
            _solution = NavSolution.Empty;
            return;
        }

        var directory = new DirectoryInfo(rootPath);

        // Dateiliste über die Standard-Discovery ermitteln, dann die Solution mit unserem
        // Overlay-Provider neu aufsetzen, damit Scan und offene Dokumente denselben Cache teilen.
        var discovered = await NavSolution.FromDirectoryAsync(directory, cancellationToken);

        _solution = new NavSolution(directory, discovered.SolutionFiles, _syntaxProvider, _semanticModelProvider);
    }

    /// <summary>Öffnet/aktualisiert ein Dokument im Overlay (Schlüssel = normalisierter Pfad).</summary>
    public void OpenOrUpdate(string normalizedPath, string text) => _syntaxProvider.SetOverlay(normalizedPath, text);

    /// <summary>Schließt ein Dokument — die Wahrheit liegt wieder auf Platte.</summary>
    public void Close(string normalizedPath) => _syntaxProvider.RemoveOverlay(normalizedPath);

    /// <summary>Ist das Dokument aktuell offen (Overlay vorhanden)? Dann schlägt das Overlay die Platte.</summary>
    public bool IsOpen(string normalizedPath) => _syntaxProvider.IsOpen(normalizedPath);

    /// <summary>
    /// Invalidiert den Platten-Syntax-Cache einer extern geänderten Datei (das Overlay bleibt unberührt) —
    /// der nächste Zugriff liest sie frisch von Platte.
    /// </summary>
    public void InvalidateDiskCache(string normalizedPath) => _syntaxProvider.InvalidateCache(normalizedPath);

    /// <summary>
    /// Nimmt eine neu angelegte Datei in die Solution auf (damit sie an solution-weiten Features teilnimmt).
    /// Re-globt NICHT, sondern erweitert die bestehende Dateiliste; ohne Workspace-Root passiert nichts.
    /// </summary>
    public void AddSolutionFile(string filePath) {

        if (_solution.SolutionDirectory == null) {
            return;
        }

        var normalized = PathHelper.NormalizePath(filePath);
        if (normalized == null) {
            return;
        }

        if (_solution.SolutionFiles.Any(f => string.Equals(PathHelper.NormalizePath(f.FullName), normalized, StringComparison.OrdinalIgnoreCase))) {
            return; // schon bekannt
        }

        var files = _solution.SolutionFiles.Add(new FileInfo(filePath));
        _solution = new NavSolution(_solution.SolutionDirectory, files, _syntaxProvider, _semanticModelProvider);
    }

    /// <summary>
    /// Entfernt eine gelöschte Datei aus der Solution und aus dem Abhängigkeitsgraphen. Kanten ANDERER Dateien,
    /// die sie inkludierten, bleiben erhalten (werden beim Neudiagnostizieren der Inkludierer aufgefrischt).
    /// </summary>
    public void RemoveSolutionFile(string normalizedPath) {

        _dependencies.Remove(normalizedPath);

        if (_solution.SolutionDirectory == null) {
            return;
        }

        var files = _solution.SolutionFiles
                             .Where(f => !string.Equals(PathHelper.NormalizePath(f.FullName), normalizedPath, StringComparison.OrdinalIgnoreCase))
                             .ToImmutableArray();

        if (files.Length != _solution.SolutionFiles.Length) {
            _solution = new NavSolution(_solution.SolutionDirectory, files, _syntaxProvider, _semanticModelProvider);
        }
    }

    /// <summary>Syntaxbaum eines Dokuments (overlay-bewusst) — Grundlage für Semantic Tokens.</summary>
    public SyntaxTree? GetSyntaxTree(string filePath, CancellationToken cancellationToken) {
        return _syntaxProvider.GetSyntax(filePath, cancellationToken)?.SyntaxTree;
    }

    /// <summary>Semantisches Modell eines Dokuments (overlay-bewusst) — Grundlage für Document Symbols.</summary>
    public CodeGenerationUnit? GetCodeGenerationUnit(string filePath, CancellationToken cancellationToken) {
        return _semanticModelProvider.GetSemanticModel(filePath, cancellationToken);
    }

    /// <summary>Diagnostics für ein einzelnes Dokument (overlay-bewusst).</summary>
    public IReadOnlyList<Diagnostic> GetDiagnostics(string filePath, CancellationToken cancellationToken) {

        var unit = _semanticModelProvider.GetSemanticModel(filePath, cancellationToken);
        if (unit == null) {
            return Array.Empty<Diagnostic>();
        }

        // Beim Diagnostizieren zugleich die Include-Kanten der Datei auffrischen (frei, da die Unit ohnehin
        // gebaut wurde) — so bleibt der Graph für jede berührte Datei aktuell.
        RecordIncludes(unit);

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
                                             Func<Uri, Lsp.Diagnostic[], Task> publishAsync,
                                             CancellationToken cancellationToken) {

        foreach (var dependentKey in _dependencies.GetDependentsClosure(changedFilePath)) {

            cancellationToken.ThrowIfCancellationRequested();

            var unit = _semanticModelProvider.GetSemanticModel(dependentKey, cancellationToken);
            if (unit == null) {
                continue;
            }

            var fullPath = unit.Syntax.SyntaxTree.SourceText.FileInfo?.FullName;
            if (string.IsNullOrEmpty(fullPath)) {
                continue;
            }

            // Kanten der inkludierenden Datei gleich mit auffrischen.
            RecordIncludes(unit);

            var lspDiagnostics = DiagnosticsComputer.FromUnit(unit, fullPath)
                                                    .Select(LspMapper.ToLsp)
                                                    .ToArray();

            await publishAsync(new Uri(fullPath), lspDiagnostics);
        }
    }

    /// <summary>Publiziert Diagnostics für sämtliche Workspace-Dateien.</summary>
    public Task PublishAllDiagnosticsAsync(Func<Uri, Lsp.Diagnostic[], Task> publishAsync, CancellationToken cancellationToken) {

        return _solution.ProcessCodeGenerationUnitsAsync(
            asyncAction: async unit => {

                var fullPath = unit.Syntax.SyntaxTree.SourceText.FileInfo?.FullName;
                if (string.IsNullOrEmpty(fullPath)) {
                    return;
                }

                // Include-Kanten beim Scan vollständig befüllen — auch für nie geöffnete Inkludierer, damit
                // ein späteres didChange deren Re-Diagnose anstoßen kann.
                RecordIncludes(unit);

                var navDiagnostics = DiagnosticsComputer.FromUnit(unit, fullPath);
                var lspDiagnostics = navDiagnostics.Select(LspMapper.ToLsp).ToArray();

                await publishAsync(new Uri(fullPath), lspDiagnostics);
            },
            startingUnit: null,
            cancellationToken: cancellationToken);
    }
}
