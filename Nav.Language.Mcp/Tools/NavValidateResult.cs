#region Using Directives

using System.Collections.Generic;
using System.Linq;

using Pharmatechnik.Nav.Language;

#endregion

namespace Pharmatechnik.Nav.Language.Mcp.Tools;

/// <summary>
/// Ergebnis von <c>nav_validate</c>. Bewusst eine schlanke, namens-/lesbarkeitsorientierte Sicht für den
/// KI-Agenten (kein LSP-DTO): Zeilen/Spalten sind hier 1-basiert (wie im Editor angezeigt), nicht 0-basiert
/// wie intern in der Engine.
/// </summary>
public sealed class NavValidateResult {

    /// <summary>Der validierte Dateipfad (wie übergeben).</summary>
    public string Path { get; set; } = "";

    /// <summary>True, wenn keine Diagnostics mit Schweregrad <c>Error</c> vorliegen.</summary>
    public bool Ok { get; set; }

    /// <summary>Anzahl der Diagnostics je Schweregrad — schneller Überblick für den Agenten.</summary>
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public int SuggestionCount { get; set; }

    /// <summary>Gesetzt, wenn die Datei nicht gefunden/nicht geparst werden konnte (dann keine Diagnostics).</summary>
    public string? Error { get; set; }

    public List<NavDiagnosticDto> Diagnostics { get; set; } = new();

    public static NavValidateResult NotFound(string path) => new() {
        Path  = path,
        Ok    = false,
        Error = "Datei nicht gefunden oder nicht als Nav-Datei parsebar."
    };

    public static NavValidateResult From(string path, List<NavDiagnosticDto> diagnostics) => new() {
        Path            = path,
        Diagnostics     = diagnostics,
        ErrorCount      = diagnostics.Count(d => d.Severity == nameof(DiagnosticSeverity.Error)),
        WarningCount    = diagnostics.Count(d => d.Severity == nameof(DiagnosticSeverity.Warning)),
        SuggestionCount = diagnostics.Count(d => d.Severity == nameof(DiagnosticSeverity.Suggestion)),
        Ok              = !diagnostics.Any(d => d.Severity == nameof(DiagnosticSeverity.Error))
    };
}

/// <summary>Eine einzelne Diagnose in der KI-Sicht (1-basierte Zeilen/Spalten).</summary>
public sealed class NavDiagnosticDto {

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

    /// <summary>
    /// Nur gesetzt, wenn die Diagnose aus einer ANDEREN Datei stammt (Cross-File). Bei Diagnostics der
    /// validierten Datei selbst bleibt das Feld leer.
    /// </summary>
    public string? File { get; set; }

    public static NavDiagnosticDto From(Diagnostic diagnostic, string documentNormalizedPath) {

        var location = diagnostic.Location;

        // File nur bei Cross-File-Diagnostics setzen: stammt die Location aus einer ANDEREN als der
        // validierten Datei. Same-File-Diagnostics tragen den (normalisierten) eigenen Pfad → bleibt leer.
        var locationPath = location.NormalizedFilePath;
        var crossFile = !string.IsNullOrEmpty(locationPath) &&
                        !string.Equals(locationPath, documentNormalizedPath, System.StringComparison.OrdinalIgnoreCase);

        return new NavDiagnosticDto {
            Severity  = diagnostic.Severity.ToString(),
            Category  = diagnostic.Descriptor.Category.ToString(),
            Code      = diagnostic.Descriptor.Id,
            Message   = diagnostic.Message,
            Line      = location.StartLine      + 1,
            Column    = location.StartCharacter + 1,
            EndLine   = location.EndLine        + 1,
            EndColumn = location.EndCharacter   + 1,
            File      = crossFile ? location.FilePath : null
        };
    }
}
