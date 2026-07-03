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
/// Hierarchischer, gitignore-kompatibler Matcher über alle <c>.navignore</c>-Dateien eines Verzeichnisbaums.
/// Eine <c>.navignore</c> in einem Verzeichnis gilt für dessen Unterbaum; eine tiefer liegende Datei schlägt
/// eine flacher liegende, und innerhalb einer Datei gewinnt die zuletzt zutreffende Regel (Negation via <c>!</c>).
/// Dient sowohl dem LSP (Diagnostics ignorierter Dateien stummschalten) als auch der Code-Generierung
/// (ignorierte Dateien überspringen).
/// </summary>
public sealed class NavIgnore {

    // Aufsteigend nach Tiefe sortiert (flach → tief), damit tiefere Urteile beim Auswerten überschreiben.
    readonly ImmutableArray<NavIgnoreFile> _files;

    NavIgnore(ImmutableArray<NavIgnoreFile> files) {
        _files = files;
    }

    public static NavIgnore Empty { get; } = new(ImmutableArray<NavIgnoreFile>.Empty);

    /// <summary>
    /// Lädt alle <c>.navignore</c>-Dateien unterhalb von <paramref name="rootDir"/> (inklusive Unterverzeichnissen).
    /// Geeignet für den LSP-Workspace und den CLI-<c>/d:</c>-Verzeichnis-Scan, bei denen die Wurzel die Scangrenze ist.
    /// </summary>
    public static NavIgnore Load(string? rootDir) {

        if (string.IsNullOrEmpty(rootDir) || !Directory.Exists(rootDir)) {
            return Empty;
        }

        var dirInfo = new DirectoryInfo(rootDir);

        var files = dirInfo.TryEnumerateFiles(".navignore", SearchOption.AllDirectories)
                           .Select(fi => NavIgnoreFile.Load(fi.FullName))
                           .Where(f => f != null)
                           .Select(f => f!)
                           .OrderBy(f => f.Depth)
                           .ToImmutableArray();

        return files.IsEmpty ? Empty : new NavIgnore(files);
    }

    /// <summary>
    /// Sammelt die <c>.navignore</c>-Dateien von <paramref name="startDir"/> aufwärts bis zur Dateisystem-Wurzel
    /// (git-artig). Geeignet für den CLI-<c>/s:</c>-Modus, in dem einzelne Quelldateien ohne gemeinsame Wurzel
    /// übergeben werden.
    /// </summary>
    public static NavIgnore LoadForAncestors(string? startDir) {

        if (string.IsNullOrEmpty(startDir)) {
            return Empty;
        }

        var files = new List<NavIgnoreFile>();

        DirectoryInfo? dir;
        try {
            dir = new DirectoryInfo(PathHelper.GetFullPathNoThrow(startDir));
        } catch (ArgumentException) {
            return Empty;
        }

        while (dir != null) {

            var candidate = Path.Combine(dir.FullName, ".navignore");
            if (File.Exists(candidate)) {
                var file = NavIgnoreFile.Load(candidate);
                if (file != null) {
                    files.Add(file);
                }
            }

            dir = dir.Parent;
        }

        if (files.Count == 0) {
            return Empty;
        }

        return new NavIgnore(files.OrderBy(f => f.Depth).ToImmutableArray());
    }

    /// <summary>
    /// <c>true</c>, wenn die Datei durch (mindestens) eine anwendbare <c>.navignore</c>-Regel ignoriert wird.
    /// </summary>
    public bool IsIgnored(string absolutePath) {

        if (_files.IsEmpty) {
            return false;
        }

        var norm = PathHelper.NormalizePath(absolutePath);
        if (norm == null) {
            return false;
        }

        bool? verdict = null;

        // Flach → tief: ein nicht-leeres Urteil einer tieferen Datei überschreibt das einer flacheren.
        foreach (var file in _files) {

            if (!TryGetRelativePath(norm, file.BaseDirNormalized, out var relativePosix)) {
                continue;
            }

            var fileVerdict = file.Match(relativePosix);
            if (fileVerdict.HasValue) {
                verdict = fileVerdict.Value;
            }
        }

        return verdict ?? false;
    }

    /// <summary>
    /// Berechnet den <c>/</c>-getrennten Pfad von <paramref name="normalizedPath"/> relativ zu
    /// <paramref name="baseDir"/>, sofern die Datei darunter liegt.
    /// </summary>
    static bool TryGetRelativePath(string normalizedPath, string baseDir, out string relativePosix) {

        relativePosix = string.Empty;

        if (string.IsNullOrEmpty(baseDir) || normalizedPath.Length <= baseDir.Length) {
            return false;
        }

        if (!normalizedPath.StartsWith(baseDir, StringComparison.Ordinal)) {
            return false;
        }

        var separator = normalizedPath[baseDir.Length];
        if (separator != '\\' && separator != '/') {
            return false; // nur ein gemeinsames Präfix, kein echtes Unterverzeichnis
        }

        relativePosix = normalizedPath.Substring(baseDir.Length)
                                      .TrimStart('\\', '/')
                                      .Replace('\\', '/');

        return relativePosix.Length > 0;
    }
}
