#region Using Directives

using System;
using System.IO;
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
/// Platten-Einträge validieren sich beim Treffer selbst über einen Datei-Stempel
/// (<see cref="DiskStamp"/>) — out-of-band geänderte Dateien werden so ohne Watcher beim nächsten
/// Zugriff bemerkt und neu geparst; Overlays bleiben autoritativ (kein Stempel).
/// VS-/LSP-frei — gemeinsam genutzt von LSP- und MCP-Server-Schale (der MCP-Server setzt keine Overlays,
/// nutzt aber Cache + Invalidierung).
/// </summary>
public class OverlaySyntaxProvider: ISyntaxProvider {

    readonly ISyntaxProvider _diskProvider;

    // Normalisierter Pfad -> Inhalt des offenen Dokuments.
    readonly ConcurrentDictionary<string, string> _overlay = new();

    // Normalisierter Pfad -> geparster Syntaxbaum (null = Datei existiert nicht) + Platten-Stempel.
    // Syntax und Stempel liegen bewusst in EINEM Eintrag: zwei getrennte Dictionaries könnten bei
    // nebenläufigen Neubauten einen frischen Stempel mit veralteter Syntax paaren — der Treffer
    // bliebe dann dauerhaft stale.
    readonly ConcurrentDictionary<string, CacheEntry> _cache = new();

    readonly record struct CacheEntry {

        public required CodeGenerationUnitSyntax? Syntax { get; init; }
        public required DiskStamp                 Stamp  { get; init; }

    }

    public OverlaySyntaxProvider(ISyntaxProvider? diskProvider = null) {
        _diskProvider = diskProvider ?? SyntaxProvider.Default;
    }

    public CodeGenerationUnitSyntax? GetSyntax(string filePath, CancellationToken cancellationToken = default) {

        var normalizedPath = PathHelper.NormalizePath(filePath)
                          ?? throw new ArgumentNullException(nameof(filePath));

        if (_overlay.TryGetValue(normalizedPath, out var text)) {

            if (_cache.TryGetValue(normalizedPath, out var cachedOverlay)) {
                return cachedOverlay.Syntax;
            }

            var parsed = Syntax.ParseCodeGenerationUnit(text: text, filePath: filePath, cancellationToken: cancellationToken);
            _cache[normalizedPath] = new CacheEntry { Syntax = parsed, Stamp = default };

            return parsed;
        }

        if (_cache.TryGetValue(normalizedPath, out var cached) &&
            cached.Stamp == DiskStamp.FromFile(normalizedPath)) {
            return cached.Syntax;
        }

        // Stempel VOR dem Lesen nehmen: ändert sich die Datei zwischen Stempel und Lesen, ist der
        // gespeicherte Stempel bereits veraltet und der nächste Zugriff parst erneut — nie umgekehrt.
        var stamp  = DiskStamp.FromFile(normalizedPath);
        var syntax = _diskProvider.GetSyntax(filePath, cancellationToken);

        _cache[normalizedPath] = new CacheEntry { Syntax = syntax, Stamp = stamp };

        return syntax;
    }

    /// <summary>Öffnet ein Dokument bzw. aktualisiert seinen Overlay-Inhalt und invalidiert den Cache.</summary>
    public void SetOverlay(string normalizedPath, string text) {
        _overlay[normalizedPath] = text;
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

    public void Dispose() {
    }

}

/// <summary>
/// Änderungs-Stempel einer Datei auf Platte (<see cref="FileSystemInfo.LastWriteTimeUtc"/> + Länge).
/// Eine fehlende (oder nicht stat-bare) Datei stempelt als <c>default</c> — damit gilt auch das
/// Wiederauftauchen einer gelöschten Datei als Änderung.
/// </summary>
readonly record struct DiskStamp {

    public required DateTime LastWriteTimeUtc { get; init; }
    public required long     Length           { get; init; }

    public static DiskStamp FromFile(string path) {
        try {
            var fileInfo = new FileInfo(path);
            return fileInfo.Exists
                ? new DiskStamp { LastWriteTimeUtc = fileInfo.LastWriteTimeUtc, Length = fileInfo.Length }
                : default;
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) {
            return default;
        }
    }

}
