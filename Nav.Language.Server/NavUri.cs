#region Using Directives

using System;

using Pharmatechnik.Nav.Utilities.IO;

#endregion

namespace Pharmatechnik.Nav.Language.Server;

/// <summary>
/// URI-Normalisierung am LSP-Rand. LSP nutzt prozent-kodierte <c>file://</c>-URIs (RFC 3986); unter
/// Windows variieren Laufwerksbuchstaben-Groß-/Kleinschreibung und <c>:</c> vs. <c>%3A</c>. Da das
/// Dateisystem case-insensitiv ist, URIs aber als Strings verglichen werden, wird am Rand einmal
/// URI → Dateipfad → <see cref="PathHelper.NormalizePath"/> umgesetzt; intern wird durchgängig mit dem
/// normalisierten Pfad als Schlüssel gearbeitet.
/// </summary>
static class NavUri {

    /// <summary>Dateipfad der URI (oder null, wenn keine file://-URI).</summary>
    public static string? ToFilePath(Uri uri) {

        if (!uri.IsFile) {
            return null;
        }

        var path = uri.LocalPath;

        // VS Code prozent-kodiert den Laufwerks-Doppelpunkt (file:///d%3A/...). System.Uri.LocalPath
        // liefert dann einen kaputten Pfad "/d:/git/..." (führender Slash, Forward-Slashes) statt
        // "d:\git\...". Führenden Slash vor "X:" entfernen und Separatoren auf Backslash normalisieren.
        if (path.Length >= 3 && path[0] == '/' && char.IsLetter(path[1]) && path[2] == ':') {
            path = path.Substring(1);
        }

        return path.Replace('/', '\\');
    }

    /// <summary>Normalisierter Schlüsselpfad der URI (oder null, wenn keine file://-URI).</summary>
    public static string? ToNormalizedPath(Uri uri) {
        var path = ToFilePath(uri);
        return path == null ? null : PathHelper.NormalizePath(path);
    }
}
