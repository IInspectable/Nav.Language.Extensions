#region Using Directives

using JetBrains.Annotations;

#endregion

namespace Pharmatechnik.Nav.Language.CodeAnalysis.FindSymbols;

/// <summary>
/// Eine benannte C#-<see cref="Location"/>: die Stelle plus der Text ihres Bezeichners, der dem Host
/// als Anzeigename dient (er trägt selbst keine Navigation). Genutzt für
/// <list type="bullet">
///   <item>Aufrufstellen der VS-freien Aufrufer-Suche <see cref="LocationFinder.FindCallerLocations"/>
///         (z.B. <c>Choice_Retry</c> für <c>next.Choice_Retry(…)</c> oder <c>BeginChild</c> für einen
///         <c>Begin{Node}(…)</c>-Wrapper);</item>
///   <item>das <c>After{Node}</c>-Rücksprungziel aus <see cref="LocationFinder.FindInitCallAfterLocation"/>
///         (Anzeigename = Methoden-Bezeichner).</item>
/// </list>
/// </summary>
public class CallerLocation: Location {

    public CallerLocation(Location location, string callerName): base(location) {
        CallerName = callerName ?? string.Empty;
    }

    [NotNull]
    public string CallerName { get; }
}
