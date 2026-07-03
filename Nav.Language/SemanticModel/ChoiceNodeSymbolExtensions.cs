#region Using Directives

using System.Collections.Generic;
using System.Linq;

#endregion

namespace Pharmatechnik.Nav.Language;

public static class ChoiceNodeSymbolExtensions {

    public static IEnumerable<Call> ExpandCalls(this IChoiceNodeSymbol? source) {
        return source == null ? Enumerable.Empty<Call>() : source.Outgoings.GetReachableCalls();
    }

}
