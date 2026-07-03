#nullable enable

#region Using Directives

using System.Collections.Generic;
using System.Linq;
using System.Threading;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes.ErrorFix; 

public static class AddMissingExitTransitionCodeFixProvider {

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