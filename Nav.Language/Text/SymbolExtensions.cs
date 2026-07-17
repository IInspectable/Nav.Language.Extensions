#region Using Directives

using System.Collections.Immutable;

#endregion

namespace Pharmatechnik.Nav.Language.Text;

/// <summary>
/// Erweiterungsmethoden, die ein <see cref="ISymbol"/> in seine klassifizierte Anzeige übersetzen
/// (Gegenstück zu Roslyns <c>ISymbol.ToDisplayParts</c>).
/// </summary>
public static class SymbolExtensions {

    /// <summary>
    /// Liefert die klassifizierte Anzeige des Symbols als Folge von <see cref="ClassifiedText"/>-Stücken
    /// (z.B. für QuickInfo/Hover). Delegiert an <see cref="DisplayPartsBuilder"/>, der je Symbolart die
    /// passenden Teile zusammensetzt; für nicht behandelte Symbolarten ist das Ergebnis leer.
    /// </summary>
    /// <param name="symbol">Das anzuzeigende Symbol.</param>
    /// <returns>Die klassifizierten Anzeigeteile des Symbols.</returns>
    public static ImmutableArray<ClassifiedText> ToDisplayParts(this ISymbol symbol) {
        return DisplayPartsBuilder.Invoke(symbol);
    }

}