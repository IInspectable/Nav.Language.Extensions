#region Using Directives

using System;
using System.Collections.Concurrent;
using System.Threading;

using Pharmatechnik.Nav.Utilities.IO;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Trefferstatistik eines <see cref="CachedSyntaxProvider"/> — zählt Cache-Treffer und -Fehlschläge.
/// Unveränderlich; die <c>With…</c>-Methoden liefern jeweils einen fortgeschriebenen Wert.
/// </summary>
public readonly struct CachedSyntaxProviderStatistic {

    /// <summary>Erzeugt eine Statistik mit den angegebenen Zählerständen.</summary>
    /// <param name="cacheHits">Die Anzahl der Cache-Treffer.</param>
    /// <param name="cacheFails">Die Anzahl der Cache-Fehlschläge.</param>
    public CachedSyntaxProviderStatistic(int cacheHits, int cacheFails) {
        CacheHits  = cacheHits;
        CacheFails = cacheFails;
    }

    /// <summary>Die Anzahl der Cache-Treffer (Datei aus dem Cache bedient).</summary>
    public int CacheHits  { get; }
    /// <summary>Die Anzahl der Cache-Fehlschläge (Datei neu geparst).</summary>
    public int CacheFails { get; }

    /// <summary>Liefert die Statistik mit um eins erhöhtem <see cref="CacheHits"/>.</summary>
    public CachedSyntaxProviderStatistic WithCacheHit() {
        return new CachedSyntaxProviderStatistic(CacheHits + 1, CacheFails);
    }

    /// <summary>Liefert die Statistik mit um eins erhöhtem <see cref="CacheFails"/>.</summary>
    public CachedSyntaxProviderStatistic WithCacheFail() {
        return new CachedSyntaxProviderStatistic(CacheHits, CacheFails + 1);
    }

}

/// <summary>
/// Cachender <see cref="ISyntaxProvider"/>-Decorator: hält je normalisiertem Dateipfad das Ergebnis
/// eines inneren Providers fest — auch das negative (nicht existierende Datei → <c>null</c>). Bietet
/// keinen Invalidate-Pfad; der Cache wird als Ganzes beim <see cref="Dispose"/> geleert. Führt eine
/// <see cref="Statistic"/> über Treffer und Fehlschläge.
/// </summary>
public class CachedSyntaxProvider: ISyntaxProvider {

    // Wert bewusst nullable: der Provider cacht auch das negative Ergebnis (nicht existierende Datei → null).
    readonly ConcurrentDictionary<string, CodeGenerationUnitSyntax?> _cache;
    readonly ISyntaxProvider                                         _syntaxProvider;

    private readonly object _gate = new();

    /// <summary>Erzeugt einen Provider über dem <see cref="SyntaxProvider.Default"/> als innerem Provider.</summary>
    public CachedSyntaxProvider(): this(null) {

    }

    /// <summary>
    /// Erzeugt einen Provider über <paramref name="syntaxProvider"/> als innerem Provider.
    /// </summary>
    /// <param name="syntaxProvider">Der innere Provider; <c>null</c> nutzt <see cref="SyntaxProvider.Default"/>.</param>
    public CachedSyntaxProvider(ISyntaxProvider? syntaxProvider) {

        _syntaxProvider = syntaxProvider ?? SyntaxProvider.Default;
        _cache          = new ConcurrentDictionary<string, CodeGenerationUnitSyntax?>();
        Statistic       = default;
    }

    /// <inheritdoc/>
    public virtual CodeGenerationUnitSyntax? GetSyntax(string filePath, CancellationToken cancellationToken = default) {

        var normalizedFilePath = PathHelper.NormalizePath(filePath);

        if (normalizedFilePath == null) {
            throw new ArgumentNullException();
        }

        if (_cache.TryGetValue(normalizedFilePath, out var syntax)) {

            CacheHit();
            return syntax;
        }

        CacheFail();

        syntax = _syntaxProvider.GetSyntax(filePath, cancellationToken);

        _cache[normalizedFilePath] = syntax;

        return syntax;
    }

    /// <summary>Die aktuelle Treffer-/Fehlschlag-Statistik des Caches.</summary>
    public CachedSyntaxProviderStatistic Statistic { get; private set; }

    /// <inheritdoc/>
    public virtual void Dispose() {
        ClearCache();
    }

    void CacheFail() {

        lock (_gate) {
            Statistic = Statistic.WithCacheFail();
        }
    }

    void CacheHit() {

        lock (_gate) {
            Statistic = Statistic.WithCacheHit();
        }
    }

    void ClearCache() {
        lock (_gate) {
            _cache.Clear();
            Statistic = default;
        }
    }

}