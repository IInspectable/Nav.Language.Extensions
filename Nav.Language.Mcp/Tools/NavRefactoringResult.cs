#region Using Directives

using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language.Mcp.Tools;

/// <summary>
/// Ergebnis von <c>nav_rename</c>. Liefert NUR das Edit-Set (1-basiert) — es wird NICHTS auf Platte
/// geschrieben; der Agent wendet die Edits selbst an. Alle Edits sind dateilokal (beziehen sich auf
/// <see cref="Path"/>), wie der VS-Rename. Ist das Symbol über Dateigrenzen sichtbar (Task-Name oder ein von
/// einer Instanz benutzter Exit), nennen <see cref="Warning"/>/<see cref="CrossFileFiles"/>/
/// <see cref="CrossFileReferenceCount"/> die anderen Dateien, die sonst still brechen. Bei ungültigem Namen /
/// Mehrdeutigkeit ist <see cref="Error"/> gesetzt.
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

    /// <summary>
    /// Der komplette Dateitext nach Anwendung der <see cref="Edits"/>. Am einfachsten die Datei damit
    /// überschreiben, statt die Edits punktgenau selbst anzuwenden. <c>null</c>, wenn nichts zu ändern ist
    /// oder der Aufrufer ihn abbestellt hat (<c>includeResultText=false</c>).
    /// </summary>
    public string? ResultText { get; set; }

    /// <summary>
    /// Anzahl der Referenzen in ANDEREN Dateien, die dieser dateilokale Rename NICHT anfasst und die dadurch
    /// brechen (NAV0010). 0, wenn das Symbol nur dateilokal sichtbar ist.
    /// </summary>
    public int CrossFileReferenceCount { get; set; }

    /// <summary>
    /// Die betroffenen anderen Dateien (absolute Pfade), in denen Referenzen manuell nachzuziehen sind. Leer,
    /// wenn der Rename keine Datei jenseits von <see cref="Path"/> berührt.
    /// </summary>
    public List<string> CrossFileFiles { get; set; } = new();

    /// <summary>
    /// Menschlich lesbare Warnung, wenn der Rename über Dateigrenzen sichtbar bricht — sonst <c>null</c>. Der
    /// Rename bleibt dateilokal (wie der VS-Rename); die genannten Dateien müssen separat nachgezogen werden
    /// (z.B. per <c>nav_references</c>/<c>nav_exit_usages</c> + erneutem <c>nav_rename</c> je Datei).
    /// </summary>
    public string? Warning { get; set; }

}

/// <summary>
/// Ergebnis von <c>nav_code_actions</c>: die am angefragten Symbol anwendbaren Aktionen samt ihrer fertigen
/// Edits (1-basiert). NUR Rückgabe — keine Plattenänderung; der Agent wählt und wendet selbst an.
/// </summary>
public sealed class NavCodeActionsResult {

    public string Path { get; set; } = "";

    /// <summary>Das abgefragte Symbol — leer, wenn die Aktionen der ganzen Datei erfragt wurden.</summary>
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

    /// <summary>
    /// Der komplette Dateitext nach Anwendung genau dieser Aktion. Am einfachsten die Datei damit
    /// überschreiben, statt die <see cref="Edits"/> punktgenau selbst anzuwenden. <c>null</c>, wenn der
    /// Aufrufer ihn abbestellt hat (<c>includeResultText=false</c>).
    /// </summary>
    public string? ResultText { get; set; }

}