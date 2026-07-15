#region Using Directives

using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Threading;

using Pharmatechnik.Nav.Utilities.IO;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Cachender Decorator über einem <see cref="ISemanticModelProvider"/> — Tier 2 über dem Syntax-Cache
/// (Tier 1). Schlüssel ist der normalisierte Dateipfad. Jeder Eintrag validiert sich beim Treffer
/// selbst (Vorwärts-Snapshot statt Reverse-Abhängigkeitsgraph): er hält die Syntax-Instanz, aus der
/// er gebaut wurde, sowie je direktem Include die Syntax-Instanz zum Bauzeitpunkt. Ein Treffer ist
/// nur gültig, wenn alle Instanzen referenzgleich zum aktuellen Stand des
/// <see cref="ISyntaxProvider"/> sind — dreht Tier 1 eine Instanz (Overlay-Edit, externe Änderung
/// via Stempel-Frische), läuft der nächste Lookup automatisch auf Neubau. Einen expliziten
/// Invalidate-Pfad gibt es bewusst nicht. Includes wirken nicht transitiv (eine Datei hängt nur an
/// sich selbst plus ihren direkten Includes), daher genügt eine Snapshot-Ebene; Zyklen konvergieren,
/// weil nur Referenzen verglichen werden. Syntaxbäume ohne Dateibezug (ungespeicherte Puffer)
/// werden ungecacht durchgereicht. Das Teilen gecachter Units ist gefahrlos, weil die
/// <see cref="CodeGenerationUnit"/> nach dem Bau unveränderlich ist.
/// </summary>
public sealed class CachedSemanticModelProvider: ISemanticModelProvider {

    readonly ISemanticModelProvider _inner;
    readonly ISyntaxProvider        _syntaxProvider;

    // Normalisierter Pfad -> gecachte Unit samt Validierungs-Snapshot.
    readonly ConcurrentDictionary<string, CacheEntry> _cache = new();

    sealed class CacheEntry {

        public required CodeGenerationUnitSyntax        PrimarySyntax { get; init; }
        public required CodeGenerationUnit              Unit          { get; init; }
        public required ImmutableArray<IncludeSnapshot> Includes      { get; init; }

    }

    // Je direktem Include: Pfad + Syntax-Instanz zum Bauzeitpunkt (null = Datei existierte nicht;
    // taucht sie wieder auf, liefert Tier 1 eine Instanz und der Referenzvergleich schlägt an).
    readonly record struct IncludeSnapshot {

        public required string                    FilePath { get; init; }
        public required CodeGenerationUnitSyntax? Syntax   { get; init; }

    }

    /// <summary>
    /// Erzeugt den cachenden Decorator über <paramref name="inner"/>, wobei <paramref name="syntaxProvider"/>
    /// (derselbe Tier-1-Provider, aus dem die Units gebaut werden) zur Validierung der Cache-Einträge
    /// herangezogen wird.
    /// </summary>
    /// <param name="inner">Der innere Provider, der die Units tatsächlich baut.</param>
    /// <param name="syntaxProvider">Der Syntax-Provider, gegen dessen Instanzen die Einträge validiert werden.</param>
    /// <exception cref="ArgumentNullException">Ein Argument ist <c>null</c>.</exception>
    public CachedSemanticModelProvider(ISemanticModelProvider inner, ISyntaxProvider syntaxProvider) {
        _inner          = inner          ?? throw new ArgumentNullException(nameof(inner));
        _syntaxProvider = syntaxProvider ?? throw new ArgumentNullException(nameof(syntaxProvider));
    }

    /// <inheritdoc/>
    public CodeGenerationUnit? GetSemanticModel(string filePath, CancellationToken cancellationToken = default) {

        var normalizedPath = PathHelper.NormalizePath(filePath);
        if (normalizedPath == null) {
            return _inner.GetSemanticModel(filePath, cancellationToken);
        }

        var syntax = _syntaxProvider.GetSyntax(filePath, cancellationToken);
        if (syntax == null) {
            // Datei existiert nicht (mehr) — einen etwaigen Alteintrag nicht weiter festhalten.
            _cache.TryRemove(normalizedPath, out _);
            return null;
        }

        return GetOrBuild(normalizedPath, syntax, cancellationToken);
    }

    /// <inheritdoc/>
    public CodeGenerationUnit GetSemanticModel(CodeGenerationUnitSyntax syntax, CancellationToken cancellationToken = default) {

        var normalizedPath = PathHelper.NormalizePath(syntax.SyntaxTree.SourceText.FileInfo?.FullName);
        if (normalizedPath == null) {
            // Ungespeicherter Puffer ohne Dateibezug — nicht pfad-adressierbar, ungecacht durchreichen.
            return _inner.GetSemanticModel(syntax, cancellationToken);
        }

        return GetOrBuild(normalizedPath, syntax, cancellationToken);
    }

    CodeGenerationUnit GetOrBuild(string normalizedPath, CodeGenerationUnitSyntax primarySyntax, CancellationToken cancellationToken) {

        if (_cache.TryGetValue(normalizedPath, out var entry) && IsValid(entry, primarySyntax, cancellationToken)) {
            return entry.Unit;
        }

        var unit = _inner.GetSemanticModel(primarySyntax, cancellationToken);

        _cache[normalizedPath] = new CacheEntry {
            PrimarySyntax = primarySyntax,
            Unit          = unit,
            Includes      = CaptureIncludeSnapshot(unit, cancellationToken),
        };

        return unit;
    }

    bool IsValid(CacheEntry entry, CodeGenerationUnitSyntax currentPrimarySyntax, CancellationToken cancellationToken) {

        if (!ReferenceEquals(entry.PrimarySyntax, currentPrimarySyntax)) {
            // Die Datei selbst hat eine neue Syntax-Instanz.
            return false;
        }

        foreach (var include in entry.Includes) {
            if (!ReferenceEquals(_syntaxProvider.GetSyntax(include.FilePath, cancellationToken), include.Syntax)) {
                // Ein direktes Include hat sich geändert (oder ist verschwunden/wiederaufgetaucht).
                return false;
            }
        }

        return true;
    }

    // Der Snapshot wird nach jedem Neubau frisch aus unit.Includes abgeleitet und driftet daher nie
    // vom tatsächlichen Include-Set weg. Ändert sich ein Include zwischen dem Lesen im Bau und der
    // Aufnahme hier, trägt der Snapshot bereits die neuere Instanz, während die Unit noch gegen die
    // ältere gebaut wurde — dieses Fenster ist auf die Baudauer einer einzelnen Datei begrenzt und
    // schließt sich mit der nächsten Änderung des Includes; im request/response-Betrieb der Hosts
    // ruhen die Dateien während des Baus.
    ImmutableArray<IncludeSnapshot> CaptureIncludeSnapshot(CodeGenerationUnit unit, CancellationToken cancellationToken) {

        var snapshot = ImmutableArray.CreateBuilder<IncludeSnapshot>();

        foreach (var include in unit.Includes) {

            // Nicht normalisierbare Pfade lassen sich nicht beobachten — solche Includes liefern
            // auch keine Deklarationen und bleiben außen vor.
            if (PathHelper.NormalizePath(include.FileName) == null) {
                continue;
            }

            snapshot.Add(new IncludeSnapshot {
                FilePath = include.FileName,
                Syntax   = _syntaxProvider.GetSyntax(include.FileName, cancellationToken),
            });
        }

        return snapshot.ToImmutable();
    }

    /// <inheritdoc/>
    public void Dispose() {
        _cache.Clear();
        _inner.Dispose();
    }

}
