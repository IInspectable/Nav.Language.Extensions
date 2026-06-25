#region Using Directives

using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language.Mcp.Tools;

/// <summary>
/// Ergebnis von <c>nav_rename</c>. Liefert NUR das Edit-Set (1-basiert) — es wird NICHTS auf Platte
/// geschrieben; der Agent wendet die Edits selbst an. Alle Edits sind dateilokal (beziehen sich auf
/// <see cref="Path"/>), wie der VS-Rename. Bei ungültigem Namen / Mehrdeutigkeit ist <see cref="Error"/> gesetzt.
/// </summary>
public sealed class NavRenameResult {

    public string Path { get; set; } = "";

    /// <summary>Der aktuelle (alte) Name des Symbols.</summary>
    public string Name { get; set; } = "";

    /// <summary>Der gewünschte neue Name.</summary>
    public string NewName { get; set; } = "";

    /// <summary>Nicht gefunden / mehrdeutig / ungültiger Name / nicht umbenennbar.</summary>
    public string? Error { get; set; }

    /// <summary>Bei Mehrdeutigkeit: die in Frage kommenden Symbole (per <c>task</c> eingrenzbar).</summary>
    public List<NavSymbolRef> Candidates { get; set; } = new();

    /// <summary>Die anzuwendenden Textänderungen (leer = nichts zu tun, z.B. gleicher Name).</summary>
    public List<NavEditDto> Edits { get; set; } = new();

}

/// <summary>
/// Ergebnis von <c>nav_code_actions</c>: die am angefragten Symbol anwendbaren Aktionen samt ihrer fertigen
/// Edits (1-basiert). NUR Rückgabe — keine Plattenänderung; der Agent wählt und wendet selbst an.
/// </summary>
public sealed class NavCodeActionsResult {

    public string Path { get; set; } = "";
    public string Name { get; set; } = "";

    public string? Error { get; set; }

    public List<NavSymbolRef> Candidates { get; set; } = new();

    public List<NavCodeActionDto> Actions { get; set; } = new();

}

/// <summary>Eine einzelne Code-Aktion in der KI-Sicht.</summary>
public sealed class NavCodeActionDto {

    public string Title { get; set; } = "";

    /// <summary>Engine-Kategorie: ErrorFix | StyleFix | Refactoring | CodeFix.</summary>
    public string Category { get; set; } = "";

    /// <summary>Grobe Art: quickfix | refactor.</summary>
    public string Kind { get; set; } = "";

    public List<NavEditDto> Edits { get; set; } = new();

}