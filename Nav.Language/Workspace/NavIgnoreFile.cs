#region Using Directives

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Pharmatechnik.Nav.Utilities.IO;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Eine geparste <c>.navignore</c>-Datei samt ihrem Basis-Verzeichnis. Die Regeln gelten für den Unterbaum
/// unterhalb von <see cref="BaseDirNormalized"/>. Innerhalb der Datei gewinnt die zuletzt zutreffende Regel
/// (gitignore: last-match-wins).
/// </summary>
sealed class NavIgnoreFile {

    readonly ImmutableArray<NavIgnorePattern> _patterns;

    NavIgnoreFile(string baseDirNormalized, ImmutableArray<NavIgnorePattern> patterns) {
        BaseDirNormalized = baseDirNormalized;
        _patterns         = patterns;
        Depth             = CountSegments(baseDirNormalized);
    }

    /// <summary>Normalisiertes Basis-Verzeichnis (kleingeschrieben, ohne abschließenden Separator).</summary>
    public string BaseDirNormalized { get; }

    /// <summary>Verzeichnis-Tiefe — für die Reihenfolge (tiefere Datei schlägt flachere).</summary>
    public int Depth { get; }

    /// <summary>
    /// Liefert das Urteil dieser Datei für einen relativen, <c>/</c>-getrennten, kleingeschriebenen Pfad:
    /// <c>true</c> = ignoriert, <c>false</c> = explizit wieder eingeschlossen (Negation), <c>null</c> = keine Regel greift.
    /// </summary>
    public bool? Match(string relativePathPosix) {

        bool? verdict = null;

        // Vorwärts laufen: die letzte zutreffende Regel bestimmt das Ergebnis.
        foreach (var pattern in _patterns) {
            if (pattern.IsMatch(relativePathPosix)) {
                verdict = !pattern.IsNegated;
            }
        }

        return verdict;
    }

    public static NavIgnoreFile? Load(string navIgnoreFilePath) {

        var baseDir = Path.GetDirectoryName(navIgnoreFilePath);
        if (string.IsNullOrEmpty(baseDir)) {
            return null;
        }

        string[] lines;
        try {
            lines = File.ReadAllLines(navIgnoreFilePath);
        } catch (IOException) {
            return null;
        } catch (UnauthorizedAccessException) {
            return null;
        }

        return FromLines(baseDir, lines);
    }

    /// <summary>Test-Einstieg ohne Datei-IO.</summary>
    public static NavIgnoreFile FromLines(string baseDir, IEnumerable<string>? lines) {

        var baseDirNormalized = (PathHelper.NormalizePath(baseDir) ?? baseDir).TrimEnd('\\', '/');

        var patterns = (lines ?? Enumerable.Empty<string>())
                      .Select(NavIgnorePattern.TryParse)
                      .Where(p => p != null)
                      .Select(p => p!)
                      .ToImmutableArray();

        return new NavIgnoreFile(baseDirNormalized, patterns);
    }

    static int CountSegments(string normalizedDir) {

        if (string.IsNullOrEmpty(normalizedDir)) {
            return 0;
        }

        var count = 0;
        foreach (var c in normalizedDir) {
            if (c == '\\' || c == '/') {
                count++;
            }
        }

        return count;
    }
}
