#region Using Directives

using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language.Mcp.Tools;

/// <summary>
/// Ergebnis von <c>nav_diagnostics</c> — das workspace-weite Gegenstück zu <c>nav_validate</c>: aggregiert
/// die Diagnostics aller (bzw. per <c>filter</c> eingegrenzten) <c>.nav</c>-Dateien der Solution. Bewusst eine
/// schlanke KI-Sicht (kein LSP-DTO): Zeilen/Spalten sind 1-basiert (wie im Editor angezeigt).
/// <para>
/// <see cref="Summary"/>, <see cref="Count"/> und <see cref="FilesWithDiagnostics"/> beziehen sich auf das
/// vollständig gefilterte Set (Pfad- + Severity-Filter) VOR dem Paging; <see cref="Offset"/>/<see cref="Limit"/>
/// schneiden daraus die zurückgegebene Seite <see cref="Diagnostics"/> heraus.
/// </para>
/// </summary>
public sealed class NavDiagnosticsResult {

    /// <summary>Wurzelverzeichnis der Solution (oder <c>null</c>, wenn nichts geladen ist).</summary>
    public string? Root { get; set; }

    /// <summary>Anzahl der Dateien, die nach Anwendung von <c>filter</c> tatsächlich validiert wurden.</summary>
    public int FilesScanned { get; set; }

    /// <summary>Davon Dateien mit mindestens einer Diagnose (nach Severity-Filter).</summary>
    public int FilesWithDiagnostics { get; set; }

    /// <summary>Gesamtzahlen je Schweregrad über das gefilterte Set (vor Paging).</summary>
    public DiagnosticsSummary Summary { get; set; } = new();

    /// <summary>Gesamtzahl der Diagnostics nach allen Filtern (vor Paging).</summary>
    public int Count { get; set; }

    /// <summary>Anzahl der Diagnostics in dieser Seite.</summary>
    public int Returned { get; set; }

    public int Offset { get; set; }
    public int Limit { get; set; }

    /// <summary>True, wenn nach dieser Seite noch weitere Diagnostics folgen (per <c>offset</c> nachladen).</summary>
    public bool Truncated { get; set; }

    /// <summary>Gesetzt, wenn keine Solution geladen werden konnte (dann keine Diagnostics).</summary>
    public string? Error { get; set; }

    public List<NavWorkspaceDiagnosticDto> Diagnostics { get; set; } = new();
}

/// <summary>Severity-Counts über das gefilterte Set.</summary>
public sealed class DiagnosticsSummary {
    public int Error { get; set; }
    public int Warning { get; set; }
    public int Suggestion { get; set; }
}

/// <summary>
/// Eine einzelne Diagnose in der workspace-weiten Sicht: wie <see cref="NavDiagnosticDto"/>, aber mit
/// Datei-Kontext — da die Liste flach über alle Dateien geht, trägt jede Diagnose ihren eigenen Pfad.
/// Zeilen/Spalten sind 1-basiert.
/// </summary>
public sealed class NavWorkspaceDiagnosticDto {

    /// <summary>Absoluter Pfad der Datei (zum Weiterreichen an andere <c>nav_*</c>-Tools).</summary>
    public string Path { get; set; } = "";

    /// <summary>Pfad relativ zur Solution-Wurzel (zur Orientierung).</summary>
    public string RelativePath { get; set; } = "";

    /// <summary>Error | Warning | Suggestion.</summary>
    public string Severity { get; set; } = "";

    /// <summary>Diagnose-Kategorie der Engine (z.B. Syntax/Semantic).</summary>
    public string Category { get; set; } = "";

    /// <summary>Diagnose-Id (z.B. "Nav...") — stabil über die Engine-Descriptoren.</summary>
    public string Code { get; set; } = "";

    public string Message { get; set; } = "";

    /// <summary>1-basierte Startzeile.</summary>
    public int Line { get; set; }
    /// <summary>1-basierte Startspalte.</summary>
    public int Column { get; set; }
    public int EndLine { get; set; }
    public int EndColumn { get; set; }

    public static NavWorkspaceDiagnosticDto From(NavDiagnosticDto diagnostic, string path, string relativePath) => new() {
        Path         = path,
        RelativePath = relativePath,
        Severity     = diagnostic.Severity,
        Category     = diagnostic.Category,
        Code         = diagnostic.Code,
        Message      = diagnostic.Message,
        Line         = diagnostic.Line,
        Column       = diagnostic.Column,
        EndLine      = diagnostic.EndLine,
        EndColumn    = diagnostic.EndColumn
    };
}
