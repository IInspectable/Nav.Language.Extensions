#region Using Directives

using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language.Mcp.Tools;

/// <summary>
/// Ergebnis von <c>nav_call_hierarchy</c>: die Aufrufbeziehungen einer Task auf Task-Ebene. Der Aufrufgraph
/// der Nav-Sprache läuft über <c>task</c>-Knoten (<c>task Sub Foo;</c>), die — auch cross-file via
/// <c>taskref</c> — die Deklaration einer aufgerufenen Task referenzieren. Bewusst eine schlanke KI-Sicht
/// (1-basierte Positionen); Engine-Kern ist <c>NavCallHierarchyService</c>, derselbe wie in der
/// LSP-Call-Hierarchy.
/// </summary>
public sealed class NavCallHierarchyResult {

    /// <summary>Die Datei der Ausgangs-Task (wie übergeben).</summary>
    public string Path { get; set; } = "";

    /// <summary>Die Ausgangs-Task, deren Aufrufbeziehungen gefragt sind.</summary>
    public string Task { get; set; } = "";

    /// <summary>Abgefragte Richtung: <c>incoming</c> | <c>outgoing</c> | <c>both</c> (echo).</summary>
    public string Direction { get; set; } = "";

    /// <summary>Gesetzt, wenn die Datei/Task nicht gefunden oder die Richtung ungültig ist.</summary>
    public string? Error { get; set; }

    /// <summary>
    /// Ausgehende Aufrufe: die von dieser Task aufgerufenen Tasks (je aufgelöstem <c>task</c>-Knoten), nach
    /// Ziel gruppiert. Nur befüllt, wenn die Richtung <c>outgoing</c>/<c>both</c> umfasst.
    /// </summary>
    public List<NavCallDto> Outgoing { get; set; } = new();

    /// <summary>
    /// Eingehende Aufrufe: solution-weit alle Tasks, die diese Task über einen <c>task</c>-Knoten aufrufen,
    /// nach Aufrufer gruppiert. Nur befüllt, wenn die Richtung <c>incoming</c>/<c>both</c> umfasst.
    /// </summary>
    public List<NavCallDto> Incoming { get; set; } = new();

    public static NavCallHierarchyResult NotFound(string path) => new() {
        Path  = path,
        Error = "Datei nicht gefunden oder nicht als Nav-Datei parsebar."
    };

}

/// <summary>
/// Eine Aufrufbeziehung zu genau einer anderen Task: das Ziel (bei <c>outgoing</c>) bzw. der Aufrufer (bei
/// <c>incoming</c>) samt der Aufrufstellen (die <c>task</c>-Knoten-Bezeichner) in der aufrufenden Task.
/// </summary>
public sealed class NavCallDto {

    /// <summary>Name der anderen Task — das aufgerufene Ziel (outgoing) bzw. der Aufrufer (incoming).</summary>
    public string Task { get; set; } = "";

    /// <summary>
    /// Position der anderen Task: bei <c>outgoing</c> die Ziel-Deklaration (kann cross-file/inkludiert sein),
    /// bei <c>incoming</c> die Definition der aufrufenden Task. 1-basiert.
    /// </summary>
    public NavLocationDto Location { get; set; } = new();

    /// <summary>
    /// Die Aufrufstellen — die Positionen der <c>task</c>-Knoten-Bezeichner in der aufrufenden Task
    /// (mehrere, wenn dieselbe Task mehrfach aufgerufen wird). 1-basiert.
    /// </summary>
    public List<NavLocationDto> CallSites { get; set; } = new();

}
