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

    /// <summary>Anzahl der gefundenen <c>.nav</c>-Dateien.</summary>
    public int FileCount { get; set; }

    public List<NavFileEntry> Files { get; set; } = new();
}

/// <summary>Eine <c>.nav</c>-Datei der Solution.</summary>
public sealed class NavFileEntry {

    /// <summary>Pfad relativ zur Solution-Wurzel (für die Anzeige/Orientierung).</summary>
    public string RelativePath { get; set; } = "";

    /// <summary>Absoluter Pfad (direkt an die anderen Tools übergebbar).</summary>
    public string Path { get; set; } = "";
}
