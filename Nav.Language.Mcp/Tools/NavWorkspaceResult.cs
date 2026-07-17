#region Using Directives

using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language.Mcp.Tools;

/// <summary>
/// Ergebnis von <c>nav_workspace</c>: Überblick über alle <c>.nav</c>-Dateien der Solution (unterhalb der
/// Workspace-Wurzel), damit der Agent den Umfang des Projekts kennt und Dateien gezielt ansteuern kann.
/// </summary>
public sealed class NavWorkspaceResult {

    /// <summary>Wurzelverzeichnis der Solution (oder <c>null</c>, wenn keine geladen wurde).</summary>
    public string? Root { get; set; }

    /// <summary>Gesamtzahl der <c>.nav</c>-Dateien in der Solution (unabhängig von Filter/Paging).</summary>
    public int FileCount { get; set; }

    /// <summary>Anzahl der Dateien, die den <c>filter</c> erfüllen (= <see cref="FileCount"/> ohne Filter).</summary>
    public int MatchCount { get; set; }

    /// <summary>Anzahl der in dieser Seite tatsächlich zurückgegebenen Einträge (= <c>Files.Count</c>).</summary>
    public int Returned { get; set; }

    /// <summary>Übersprungene Treffer (Paging-Offset).</summary>
    public int Offset { get; set; }

    /// <summary>Maximale Seitengröße dieser Antwort.</summary>
    public int Limit { get; set; }

    /// <summary>
    /// <c>true</c>, wenn jenseits dieser Seite weitere Treffer existieren — dann <c>offset</c> erhöhen oder
    /// per <c>filter</c> eingrenzen. Verhindert, dass bei großen Solutions die Antwort das Token-Limit sprengt.
    /// </summary>
    public bool Truncated { get; set; }

    public List<NavFileEntry> Files { get; set; } = new();
}

/// <summary>Eine <c>.nav</c>-Datei der Solution.</summary>
public sealed class NavFileEntry {

    /// <summary>Pfad relativ zur Solution-Wurzel (für die Anzeige/Orientierung).</summary>
    public string RelativePath { get; set; } = "";

    /// <summary>Absoluter Pfad (direkt an die anderen Tools übergebbar).</summary>
    public string Path { get; set; } = "";
}
