#region Using Directives

using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language.Mcp.Tools;

/// <summary>
/// Ergebnis von <c>nav_exit_usages</c>: solution-weit alle Stellen, an denen ein Exit einer Task über eine
/// Instanz benutzt wird — die <c>Instanz:&lt;exit&gt; --&gt; …</c>-Kanten in den aufrufenden Tasks. Das ist der
/// echte Rename-Blast-Radius eines Exits, den <c>nav_references</c> NICHT liefert (dort erscheinen nur die
/// dateilokalen eingehenden Kanten des Exit-Knotens). Bewusst eine schlanke KI-Sicht (1-basierte Positionen);
/// Engine-Kern ist <c>NavCallHierarchyService.GetExitUsagesAsync</c>.
///
/// <para>
/// Nach aufrufender Task gruppiert und — analog <c>nav_call_hierarchy</c> — über <c>filter</c>/<c>limit</c>/
/// <c>offset</c> gefiltert und paginiert (siehe <see cref="MatchCount"/>/<see cref="Truncated"/>).
/// </para>
/// </summary>
public sealed class NavExitUsagesResult {

    /// <summary>Die Datei der Task, deren Exit-Nutzungen gefragt sind (wie übergeben).</summary>
    public string Path { get; set; } = "";

    /// <summary>Die Task, deren Exits gefragt sind.</summary>
    public string Task { get; set; } = "";

    /// <summary>Der abgefragte Exit-Name — leer, wenn nach ALLEN Exits der Task gefragt wurde (echo).</summary>
    public string Exit { get; set; } = "";

    /// <summary>Gesetzt, wenn die Datei/Task nicht gefunden ist.</summary>
    public string? Error { get; set; }

    /// <summary>Gesamtzahl der aufrufenden Tasks mit Exit-Nutzungen (solution-weit, vor <c>filter</c>/Paging).</summary>
    public int CallerCount { get; set; }

    /// <summary>Gesamtzahl der einzelnen Exit-Nutzungs-Kanten (solution-weit, vor <c>filter</c>/Paging).</summary>
    public int SiteCount { get; set; }

    /// <summary>Anzahl der aufrufenden Tasks, die den <c>filter</c> erfüllen (= <see cref="CallerCount"/> ohne Filter).</summary>
    public int MatchCount { get; set; }

    /// <summary>Anzahl der in dieser Seite tatsächlich zurückgegebenen aufrufenden Tasks (= <c>Usages.Count</c>).</summary>
    public int Returned { get; set; }

    /// <summary>Übersprungene aufrufende Tasks (Paging-Offset).</summary>
    public int Offset { get; set; }

    /// <summary>Maximale Seitengröße dieser Antwort (bezogen auf die aufrufenden Tasks).</summary>
    public int Limit { get; set; }

    /// <summary>
    /// <c>true</c>, wenn jenseits dieser Seite weitere aufrufende Tasks existieren — dann <c>offset</c> erhöhen
    /// oder per <c>filter</c> eingrenzen.
    /// </summary>
    public bool Truncated { get; set; }

    /// <summary>
    /// Die Exit-Nutzungen, nach aufrufender Task gruppiert. Gefiltert und paginiert (siehe
    /// <see cref="MatchCount"/>/<see cref="Truncated"/>).
    /// </summary>
    public List<NavExitUsageDto> Usages { get; set; } = new();

    public static NavExitUsagesResult NotFound(string path) => new() {
        Path  = path,
        Error = "Datei nicht gefunden oder nicht als Nav-Datei parsebar."
    };

}

/// <summary>
/// Alle Exit-Nutzungen durch EINE aufrufende Task: die Task samt Definitions-Position und die einzelnen
/// <c>Instanz:Exit --&gt; …</c>-Kanten.
/// </summary>
public sealed class NavExitUsageDto {

    /// <summary>Name der aufrufenden Task, in der die Exit-Nutzungen stehen.</summary>
    public string Caller { get; set; } = "";

    /// <summary>Position der Definition der aufrufenden Task. 1-basiert (trägt den Dateipfad).</summary>
    public NavLocationDto Location { get; set; } = new();

    /// <summary>Anzahl der Exit-Nutzungs-Kanten in dieser Task.</summary>
    public int SiteCount { get; set; }

    /// <summary>
    /// Die einzelnen <c>Instanz:Exit --&gt; …</c>-Kanten. Ihre Datei ist stets <see cref="Location"/><c>.File</c>
    /// (die Datei der aufrufenden Task).
    /// </summary>
    public List<NavExitEdgeDto> Sites { get; set; } = new();

}

/// <summary>Eine einzelne <c>Instanz:Exit --&gt; …</c>-Kante in der KI-Sicht (Position ohne Dateipfad).</summary>
public sealed class NavExitEdgeDto {

    /// <summary>Der benutzte Exit-Name (z.B. <c>AccessDenied</c>).</summary>
    public string Exit { get; set; } = "";

    /// <summary>Die Instanz links des <c>:</c> (der <c>task</c>-Knoten-Name); leer, wenn nicht aufgelöst.</summary>
    public string Instance { get; set; } = "";

    /// <summary>Position des Exit-Bezeichners in der aufrufenden Kante (Datei = <see cref="NavExitUsageDto.Location"/><c>.File</c>).</summary>
    public NavPositionDto Position { get; set; } = new();

}
