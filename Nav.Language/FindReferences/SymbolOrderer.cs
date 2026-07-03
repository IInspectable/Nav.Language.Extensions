#nullable enable

#region Using Directives

using System.Linq;
using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language.FindReferences;

static class SymbolOrderer {

    public static IOrderedEnumerable<T> OrderByLocation<T>(this IEnumerable<T> symbols) where T : ISymbol {
        return symbols.OrderBy(s => s.Location.StartLine).ThenBy(s => s.Location.StartCharacter);

    }

}