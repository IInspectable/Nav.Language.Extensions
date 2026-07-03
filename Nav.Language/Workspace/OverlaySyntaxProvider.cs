#region Using Directives

using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;

using Pharmatechnik.Nav.Utilities.IO;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Overlay-fähiger Syntax-Provider nach dem LSP-Prinzip „offenes Dokument schlägt Platte".
/// Für offene Dokumente wird der vom Client gelieferte (ggf. ungespeicherte) Inhalt geparst, für alle
/// übrigen Dateien liest der innere Provider von Platte. Zugleich dient der Provider als Workspace-Cache
/// (Schlüssel = normalisierter Pfad) mit expliziter Invalidierung bei Overlay-Änderungen.
/// VS-/LSP-frei — gemeinsam genutzt von LSP- und MCP-Server-Schale (der MCP-Server setzt keine Overlays,
/// nutzt aber Cache + Invalidierung).
/// </summary>
public class OverlaySyntaxProvider: ISyntaxProvider {

    readonly ISyntaxProvider _diskProvider;

    // Normalisierter Pfad -> Inhalt des offenen Dokuments.
    readonly ConcurrentDictionary<string, string> _overlay = new();

    // Normalisierter Pfad -> geparster Syntaxbaum (Cache; null = Datei existiert nicht).
    readonly ConcurrentDictionary<string, CodeGenerationUnitSyntax?> _cache = new();

    public OverlaySyntaxProvider(ISyntaxProvider? diskProvider = null) {
        _diskProvider = diskProvider ?? SyntaxProvider.Default;
    }

    public CodeGenerationUnitSyntax GetSyntax(string filePath, CancellationToken cancellationToken = default) {

        var normalizedPath = PathHelper.NormalizePath(filePath)
                          ?? throw new System.ArgumentNullException(nameof(filePath));

        if (_cache.TryGetValue(normalizedPath, out var cached)) {
            return cached!;
        }

        var syntax = _overlay.TryGetValue(normalizedPath, out var text)
            ? Syntax.ParseCodeGenerationUnit(text: text, filePath: filePath, cancellationToken: cancellationToken)
            : _diskProvider.GetSyntax(filePath, cancellationToken);

        _cache[normalizedPath] = syntax;

        return syntax!;
    }

    /// <summary>Öffnet ein Dokument bzw. aktualisiert seinen Overlay-Inhalt und invalidiert den Cache.</summary>
    public void SetOverlay(string normalizedPath, string text) {
        _overlay[normalizedPath]  = text;
        _cache.TryRemove(normalizedPath, out _);
    }

    /// <summary>Verwirft das Overlay (Dokument geschlossen) — die Wahrheit liegt wieder auf Platte.</summary>
    public void RemoveOverlay(string normalizedPath) {
        _overlay.TryRemove(normalizedPath, out _);
        _cache.TryRemove(normalizedPath, out _);
    }

    /// <summary>
    /// Invalidiert NUR den (Platten-)Syntax-Cache einer Datei — ohne das Overlay anzutasten. Für externe
    /// Datei-Änderungen (<c>workspace/didChangeWatchedFiles</c>): der nächste <see cref="GetSyntax"/> liest
    /// die Datei frisch von Platte (bzw. liefert <c>null</c>, falls sie gelöscht wurde).
    /// </summary>
    public void InvalidateCache(string normalizedPath) {
        _cache.TryRemove(normalizedPath, out _);
    }

    public bool IsOpen(string normalizedPath) => _overlay.ContainsKey(normalizedPath);

    /// <summary>Die normalisierten Pfade aller aktuell offenen Dokumente (Overlay-Schlüssel).</summary>
    public IEnumerable<string> OpenDocuments => _overlay.Keys;

    public void Dispose() { }
}
