#region Using Directives

using System.Collections.Generic;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.Mcp.Tools;

/// <summary>
/// Eine Quell-Position in der KI-Sicht: Datei plus 1-basierte Zeile/Spalte (wie im Editor angezeigt,
/// nicht 0-basiert wie intern). Gemeinsam genutzt von <c>nav_goto</c> und <c>nav_references</c>.
/// </summary>
public sealed class NavLocationDto {

    public string File { get; set; } = "";

    /// <summary>1-basierte Startzeile.</summary>
    public int Line { get; set; }

    /// <summary>1-basierte Startspalte.</summary>
    public int Column { get; set; }

    public int EndLine   { get; set; }
    public int EndColumn { get; set; }

    /// <summary>True, wenn dies die Deklaration selbst ist (nur bei <c>nav_references</c> gesetzt).</summary>
    public bool IsDeclaration { get; set; }

    public static NavLocationDto From(Location location, bool isDeclaration = false) => new() {
        File          = location.FilePath ?? "",
        Line          = location.StartLine      + 1,
        Column        = location.StartCharacter + 1,
        EndLine       = location.EndLine        + 1,
        EndColumn     = location.EndCharacter   + 1,
        IsDeclaration = isDeclaration
    };

}

/// <summary>
/// Eine einzelne Textänderung in der KI-Sicht: der zu ersetzende 1-basierte Bereich plus der neue Text.
/// Die mutierenden Tools (<c>nav_rename</c>, <c>nav_code_actions</c>) liefern Edits NUR zurück — sie schreiben
/// NICHTS auf Platte; der Agent wendet sie selbst an. Alle Edits beziehen sich auf die abgefragte Datei.
/// </summary>
public sealed class NavEditDto {

    /// <summary>1-basierte Startzeile des zu ersetzenden Bereichs.</summary>
    public int Line { get; set; }

    /// <summary>1-basierte Startspalte des zu ersetzenden Bereichs.</summary>
    public int Column { get; set; }

    public int EndLine   { get; set; }
    public int EndColumn { get; set; }

    /// <summary>Der einzufügende Text (leer = der Bereich wird gelöscht).</summary>
    public string NewText { get; set; } = "";

    public static NavEditDto From(SourceText sourceText, TextChange change) {

        var location = sourceText.GetLocation(change.Extent);

        return new NavEditDto {
            Line      = location.StartLine      + 1,
            Column    = location.StartCharacter + 1,
            EndLine   = location.EndLine        + 1,
            EndColumn = location.EndCharacter   + 1,
            NewText   = change.ReplacementText
        };
    }

    /// <summary>
    /// Bildet eine Folge offset-basierter Engine-<see cref="TextChange"/> auf 1-basierte Edits ab. Leere
    /// Änderungen und solche außerhalb des Textes werden übersprungen (wie der LSP-Server).
    /// </summary>
    public static List<NavEditDto> FromChanges(SourceText sourceText, IEnumerable<TextChange> changes) {

        var edits = new List<NavEditDto>();

        foreach (var change in changes) {
            if (change.IsEmpty || change.Extent.End > sourceText.Length) {
                continue;
            }

            edits.Add(From(sourceText, change));
        }

        return edits;
    }

}

/// <summary>
/// Ein Symbol-Kandidat für die Disambiguierung: liefern die name-basierten Tools mehrere Treffer gleichen
/// Namens (z.B. ein Knotenname in mehreren Tasks), beschreiben diese Refs die Kandidaten, damit der Agent
/// per <c>task</c>-Parameter eingrenzen kann.
/// </summary>
public sealed class NavSymbolRef {

    public string Name { get; set; } = "";

    /// <summary>Art des Symbols: task | init | exit | end | choice | gui | tasknode | node.</summary>
    public string Kind { get; set; } = "";

    /// <summary>Enthaltende Task-Definition (nur bei Knoten gesetzt) — als <c>task</c>-Scope verwendbar.</summary>
    public string? Task { get; set; }

    public string File { get; set; } = "";

    /// <summary>1-basierte Zeile.</summary>
    public int Line { get; set; }

    /// <summary>1-basierte Spalte.</summary>
    public int Column { get; set; }

    public static NavSymbolRef From(ISymbol symbol) => new() {
        // `?? ""` am DTO-Rand (Leitlinie String-Property non-null).
        // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
        Name   = symbol.Name ?? "",
        Kind   = NavSymbolKind.Of(symbol),
        Task   = (symbol as INodeSymbol)?.ContainingTask.Name,
        File   = symbol.Location.FilePath ?? "",
        Line   = symbol.Location.StartLine      + 1,
        Column = symbol.Location.StartCharacter + 1
    };

}