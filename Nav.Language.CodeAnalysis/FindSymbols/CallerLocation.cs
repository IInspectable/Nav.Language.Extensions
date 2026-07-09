#region Using Directives

using JetBrains.Annotations;

#endregion

namespace Pharmatechnik.Nav.Language.CodeAnalysis.FindSymbols;

/// <summary>
/// Eine C#-Aufrufstelle (<see cref="Location"/>) samt dem Text des dort aufgerufenen Identifiers
/// (z.B. <c>Choice_Retry</c> für <c>next.Choice_Retry(…)</c> oder <c>BeginChild</c> für einen
/// <c>Begin{Node}(…)</c>-Wrapper) — Ergebnis der VS-freien Aufrufer-Suche
/// <see cref="LocationFinder.FindCallerLocations"/>. Der Identifier-Text trägt keine Navigation, er
/// dient dem Host als Anzeigename der Aufrufstelle.
/// </summary>
public class CallerLocation: Location {

    public CallerLocation(Location location, string callerName): base(location) {
        CallerName = callerName ?? string.Empty;
    }

    [NotNull]
    public string CallerName { get; }
}
