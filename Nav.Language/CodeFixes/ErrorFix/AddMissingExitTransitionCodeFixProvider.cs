#region Using Directives

using System.Collections.Generic;
using System.Linq;
using System.Threading;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes.ErrorFix; 

/// <summary>
/// Findet die anwendbaren <see cref="AddMissingExitTransitionCodeFix"/> zum betroffenen Bereich. Besucht die
/// dort liegenden Symbole (<see cref="CodeFixContext.FindSymbols(bool)"/>); für jede Knoten-Referenz auf einen
/// eingebetteten Task-Knoten wird je offenem Exit-Verbindungspunkt (<c>Nav0025</c>,
/// <see cref="TaskNodeSymbolExtensions.GetUnconnectedExits"/>) ein Fix erzeugt und über
/// <see cref="AddMissingExitTransitionCodeFix.CanApplyFix"/> gefiltert.
/// </summary>
public static class AddMissingExitTransitionCodeFixProvider {

    /// <summary>
    /// Liefert für den betroffenen Bereich alle anwendbaren <see cref="AddMissingExitTransitionCodeFix"/> —
    /// je unverbundenem Exit-Verbindungspunkt eines dort referenzierten Task-Knotens einen.
    /// </summary>
    /// <param name="context">Der Fix-Kontext (Bereich, Semantik-Modell, Editor-Einstellungen).</param>
    /// <param name="cancellationToken">Token zum Abbruch der Suche.</param>
    /// <returns>Die anwendbaren Fixes; leer, wenn keine offene Exit-Transition im Bereich liegt.</returns>
    public static IEnumerable<AddMissingExitTransitionCodeFix> SuggestCodeFixes(CodeFixContext context, CancellationToken cancellationToken) {

        var visitor = new Visitor(context);

        return context.FindSymbols()
                      .Select(symbol => visitor.Visit(symbol))
                      .SelectMany(codeFixes => codeFixes)
                      .Where(codeFix => codeFix != null && codeFix.CanApplyFix());
    }

    sealed class Visitor: SymbolVisitor<IEnumerable<AddMissingExitTransitionCodeFix>> {

        public Visitor(CodeFixContext context) {
            Context = context;
        }

        CodeFixContext Context { get; }

        protected override IEnumerable<AddMissingExitTransitionCodeFix> DefaultVisit(ISymbol symbol) {
            yield break;
        }

        public override IEnumerable<AddMissingExitTransitionCodeFix> VisitNodeReferenceSymbol(INodeReferenceSymbol nodeReferenceSymbol) {

            // Add Missing Edge
            if (nodeReferenceSymbol.Declaration is ITaskNodeSymbol taskNode) {
                foreach (var missingExitConnectionPoint in taskNode.GetUnconnectedExits()) {
                    yield return new AddMissingExitTransitionCodeFix(nodeReferenceSymbol, missingExitConnectionPoint, Context);
                }
            }
        }

    }

}