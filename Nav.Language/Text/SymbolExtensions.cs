#nullable enable

#region Using Directives

using System.Collections.Immutable;

#endregion

namespace Pharmatechnik.Nav.Language.Text;

public static class SymbolExtensions {

    public static ImmutableArray<ClassifiedText> ToDisplayParts(this ISymbol symbol) {
        return DisplayPartsBuilder.Invoke(symbol);
    }

}