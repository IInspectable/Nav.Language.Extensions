#region Using Directives

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Pharmatechnik.Nav.Utilities.IO;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// VS-/LSP-freier Kern der Workspace-Verwaltung: hält die geladene <see cref="NavSolution"/> (alle <c>*.nav</c>
/// unterhalb der Wurzel) samt overlay-fähigem Syntax-Provider und liefert Syntaxbaum, semantisches Modell und
/// Diagnostics overlay-bewusst. Gemeinsame „eine Engine"-Host-Schicht: die LSP-Server-Schale (Push-Modell,
/// Overlays für ungespeicherte Editor-Puffer, Abhängigkeits-Re-Diagnose, <c>.navignore</c>) und die MCP-Schale
/// (request/response gegen den Stand auf Platte) bauen beide darauf auf.
/// </summary>
public sealed class NavWorkspaceCore {

    readonly OverlaySyntaxProvider  _syntaxProvider;
    readonly ISemanticModelProvider _semanticModelProvider;

    NavSolution _solution = NavSolution.Empty;

    public NavWorkspaceCore() {
        _syntaxProvider = new OverlaySyntaxProvider();
        // Tier-2-Semantik-Cache über dem Syntax-Cache des OverlaySyntaxProviders (Tier 1): Wiederhol-Scans
        // solution-weiter Features (Diagnostics-Sweep, Referenzen, Call Hierarchy) liefern gecachte Units,
        // solange weder die Datei selbst noch eines ihrer direkten Includes eine neue Syntax-Instanz hat.
        // Bewusst nur hier in der Host-Schicht (LSP + MCP) — die CLI (NavSolution-Default) bleibt ungecacht.
        _semanticModelProvider = new CachedSemanticModelProvider(new SemanticModelProvider(_syntaxProvider), _syntaxProvider);
    }

    /// <summary>Die geladene Solution (alle <c>*.nav</c>) — Grundlage für solution-weite Features.</summary>
    public NavSolution Solution => _solution;

    public int FileCount => _solution.SolutionFiles.Length;

    /// <summary>Wurzelverzeichnis der geladenen Solution (oder <c>null</c>, wenn nichts geladen ist).</summary>
    public DirectoryInfo? SolutionDirectory => _solution.SolutionDirectory;

    /// <summary>
    /// Lädt alle <c>*.nav</c> unterhalb von <paramref name="rootPath"/> und setzt die Solution mit dem
    /// overlay-fähigen Provider neu auf (Scan und offene Dokumente teilen denselben Cache).
    /// </summary>
    public async Task LoadAsync(string? rootPath, CancellationToken cancellationToken) {

        if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath)) {
            _solution = NavSolution.Empty;
            return;
        }

        var directory  = new DirectoryInfo(rootPath);
        var discovered = await NavSolution.FromDirectoryAsync(directory, cancellationToken);

        _solution = new NavSolution(directory, discovered.SolutionFiles, _syntaxProvider, _semanticModelProvider);
    }

    /// <summary>Syntaxbaum eines Dokuments (overlay-bewusst).</summary>
    public SyntaxTree? GetSyntaxTree(string filePath, CancellationToken cancellationToken = default) {
        return _syntaxProvider.GetSyntax(filePath, cancellationToken)?.SyntaxTree;
    }

    /// <summary>Semantisches Modell eines Dokuments (overlay-bewusst).</summary>
    public CodeGenerationUnit? GetCodeGenerationUnit(string filePath, CancellationToken cancellationToken = default) {
        return _semanticModelProvider.GetSemanticModel(filePath, cancellationToken);
    }

    /// <summary>Diagnostics für ein einzelnes Dokument (overlay-bewusst); leere Liste, wenn nicht parsebar.</summary>
    public IReadOnlyList<Diagnostic> GetDiagnostics(string filePath, CancellationToken cancellationToken = default) {

        var unit = _semanticModelProvider.GetSemanticModel(filePath, cancellationToken);
        if (unit == null) {
            return Array.Empty<Diagnostic>();
        }

        return DiagnosticsComputer.FromUnit(unit, filePath);
    }

    // --- Overlay / Cache -------------------------------------------------------------------------------

    /// <summary>Öffnet/aktualisiert ein Dokument im Overlay (Schlüssel = normalisierter Pfad).</summary>
    public void OpenOrUpdate(string normalizedPath, string text) => _syntaxProvider.SetOverlay(normalizedPath, text);

    /// <summary>Schließt ein Dokument — die Wahrheit liegt wieder auf Platte.</summary>
    public void Close(string normalizedPath) => _syntaxProvider.RemoveOverlay(normalizedPath);

    /// <summary>Ist das Dokument aktuell offen (Overlay vorhanden)?</summary>
    public bool IsOpen(string normalizedPath) => _syntaxProvider.IsOpen(normalizedPath);

    /// <summary>Die normalisierten Pfade aller aktuell offenen Dokumente.</summary>
    public IEnumerable<string> OpenDocuments => _syntaxProvider.OpenDocuments;

    /// <summary>
    /// Invalidiert den Platten-Syntax-Cache einer Datei (das Overlay bleibt unberührt) — der nächste Zugriff
    /// liest sie frisch von Platte.
    /// </summary>
    public void InvalidateCache(string normalizedPath) => _syntaxProvider.InvalidateCache(normalizedPath);

    // --- Solution-Mutation (inkrementell, ohne Re-Globben) ---------------------------------------------

    /// <summary>
    /// Nimmt eine neu angelegte Datei in die Solution auf (damit sie an solution-weiten Features teilnimmt).
    /// Re-globt NICHT; ohne Workspace-Root passiert nichts.
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

    /// <summary>Entfernt eine gelöschte Datei aus der Solution (Graph-Kanten verwaltet die aufrufende Schale).</summary>
    public void RemoveSolutionFile(string normalizedPath) {

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
}
