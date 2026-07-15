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

    /// <summary>
    /// Erzeugt eine benannte Aufrufer-Location aus der zugrunde liegenden <see cref="Location"/> und dem
    /// Anzeigenamen. Ein <c>null</c>-<paramref name="callerName"/> wird zu <see cref="string.Empty"/>
    /// normalisiert.
    /// </summary>
    /// <param name="location">Die eigentliche Location im generierten Code.</param>
    /// <param name="callerName">Der Text des Bezeichners, der als Anzeigename dient.</param>
    public CallerLocation(Location location, string callerName): base(location) {
        CallerName = callerName ?? string.Empty;
    }

    /// <summary>
    /// Der Anzeigename dieser Aufrufstelle — der Bezeichnertext (z.B. <c>Choice_Retry</c> oder
    /// <c>BeginChild</c>); nie <c>null</c>.
    /// </summary>
    [NotNull]
    public string CallerName { get; }
}
