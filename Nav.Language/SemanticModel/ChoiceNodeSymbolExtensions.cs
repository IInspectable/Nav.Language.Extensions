#region Using Directives

using System.Collections.Generic;
using System.Linq;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>Erweiterungen auf <see cref="IChoiceNodeSymbol"/>.</summary>
public static class ChoiceNodeSymbolExtensions {

    /// <summary>
    /// Faltet die Choice platt: liefert die über ihre ausgehenden Kanten
    /// (<see cref="IChoiceNodeSymbol.Outgoings"/>) erreichbaren <see cref="Call"/>s — transitiv über
    /// weitere Choices, die Choice selbst erscheint nie als Call (siehe
    /// <see cref="EdgeExtensions.GetReachableCalls(IEnumerable{IEdge})"/>); leer bei <c>null</c>.
    /// </summary>
    /// <param name="source">Der Choice-Knoten, dessen Aufrufe expandiert werden, oder <c>null</c>.</param>
    public static IEnumerable<Call> ExpandCalls(this IChoiceNodeSymbol? source) {
        return source == null ? Enumerable.Empty<Call>() : source.Outgoings.GetReachableCalls();
    }

}
