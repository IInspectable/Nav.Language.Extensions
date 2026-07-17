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
///
/// <para>
/// Zwei Detailgrade (<c>detail</c>): <b>summary</b> (Default) liefert je Beziehung nur Task, Definitions-Position
/// und <see cref="NavCallDto.CallSiteCount"/> — genug für „wer ruft X auf" / „was ruft X auf"; <b>full</b>
/// liefert zusätzlich jede einzelne Call-Site-Position. Die <b>eingehenden</b> Aufrufe (solution-weit, potentiell
/// sehr viele) sind über <c>filter</c>/<c>limit</c>/<c>offset</c> gefiltert und paginiert (siehe
/// <see cref="MatchCount"/>/<see cref="Truncated"/>); die <b>ausgehenden</b> sind durch den Task-Rumpf begrenzt
/// und werden stets vollständig zurückgegeben.
/// </para>
/// </summary>
public sealed class NavCallHierarchyResult {

    /// <summary>Die Datei der Ausgangs-Task (wie übergeben).</summary>
    public string Path { get; set; } = "";

    /// <summary>Die Ausgangs-Task, deren Aufrufbeziehungen gefragt sind.</summary>
    public string Task { get; set; } = "";

    /// <summary>Abgefragte Richtung: <c>incoming</c> | <c>outgoing</c> | <c>both</c> (echo).</summary>
    public string Direction { get; set; } = "";

    /// <summary>Gesetzt, wenn die Datei/Task nicht gefunden oder ein Argument ungültig ist.</summary>
    public string? Error { get; set; }

    /// <summary>Gesamtzahl der eingehenden Aufrufer-Tasks (solution-weit, vor <c>filter</c>/Paging).</summary>
    public int CallerCount { get; set; }

    /// <summary>Gesamtzahl der ausgehenden aufgerufenen Tasks (Callees).</summary>
    public int CalleeCount { get; set; }

    /// <summary>Gesamtzahl der eingehenden Call-Sites (<c>task</c>-Knoten aller Aufrufer, vor <c>filter</c>/Paging).</summary>
    public int CallSiteCount { get; set; }

    /// <summary>Anzahl der eingehenden Aufrufer, die den <c>filter</c> erfüllen (= <see cref="CallerCount"/> ohne Filter).</summary>
    public int MatchCount { get; set; }

    /// <summary>Anzahl der in dieser Seite tatsächlich zurückgegebenen eingehenden Aufrufer (= <c>Incoming.Count</c>).</summary>
    public int Returned { get; set; }

    /// <summary>Übersprungene eingehende Aufrufer (Paging-Offset).</summary>
    public int Offset { get; set; }

    /// <summary>Maximale Seitengröße dieser Antwort (bezogen auf die eingehenden Aufrufer).</summary>
    public int Limit { get; set; }

    /// <summary>
    /// <c>true</c>, wenn jenseits dieser Seite weitere eingehende Aufrufer existieren — dann <c>offset</c>
    /// erhöhen oder per <c>filter</c> eingrenzen. Verhindert, dass stark genutzte Tasks (viele Aufrufer) die
    /// Antwort über das Tool-Result-Token-Limit treiben.
    /// </summary>
    public bool Truncated { get; set; }

    /// <summary>
    /// Ausgehende Aufrufe: die von dieser Task aufgerufenen Tasks (je aufgelöstem <c>task</c>-Knoten), nach
    /// Ziel gruppiert. Nur befüllt, wenn die Richtung <c>outgoing</c>/<c>both</c> umfasst. Stets vollständig
    /// (durch den Task-Rumpf begrenzt); der <c>detail</c>-Modus steuert, ob die Call-Sites enthalten sind.
    /// </summary>
    public List<NavCallDto> Outgoing { get; set; } = new();

    /// <summary>
    /// Eingehende Aufrufe: solution-weit alle Tasks, die diese Task über einen <c>task</c>-Knoten aufrufen,
    /// nach Aufrufer gruppiert. Nur befüllt, wenn die Richtung <c>incoming</c>/<c>both</c> umfasst. Gefiltert
    /// und paginiert (siehe <see cref="MatchCount"/>/<see cref="Truncated"/>).
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
    /// bei <c>incoming</c> die Definition der aufrufenden Task. 1-basiert (trägt den Dateipfad).
    /// </summary>
    public NavLocationDto Location { get; set; } = new();

    /// <summary>Anzahl der Call-Sites dieser Beziehung — auch im <c>summary</c>-Modus gesetzt.</summary>
    public int CallSiteCount { get; set; }

    /// <summary>
    /// Die Call-Sites — die Positionen der <c>task</c>-Knoten-Bezeichner in der AUFRUFENDEN Task (mehrere,
    /// wenn dieselbe Task mehrfach aufgerufen wird), als kompakte Position ohne Dateipfad: die Datei ist bei
    /// <c>incoming</c> <see cref="Location"/><c>.File</c> (die Datei des Aufrufers), bei <c>outgoing</c> die
    /// abgefragte Datei (<c>path</c>). Nur im <c>detail=full</c>-Modus befüllt (sonst leer;
    /// <see cref="CallSiteCount"/> nennt trotzdem die Anzahl).
    /// </summary>
    public List<NavPositionDto> CallSites { get; set; } = new();

}
