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
    public static string? ToFilePath(Uri uri) => uri.IsFile ? uri.LocalPath : null;

    /// <summary>Normalisierter Schlüsselpfad der URI (oder null, wenn keine file://-URI).</summary>
    public static string? ToNormalizedPath(Uri uri) => uri.IsFile ? PathHelper.NormalizePath(uri.LocalPath) : null;
}
