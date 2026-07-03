#region Using Directives

using System.Collections.Generic;
using System.Linq;
using System.Threading;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes.Refactoring; 

public static class IntroduceChoiceCodeFixProvider {

    public static IEnumerable<IntroduceChoiceCodeFix> SuggestCodeFixes(CodeFixContext context, CancellationToken cancellationToken) {
        var visitor = new Visitor(context);

        return context.FindSymbols()
                      .Select(symbol => visitor.Visit(symbol))
                      .SelectMany(s => s)
                      .Where(codeFix => codeFix != null && codeFix.CanApplyFix());
    }

    sealed class Visitor: SymbolVisitor<IEnumerable<IntroduceChoiceCodeFix>> {

        public Visitor(CodeFixContext context) {
            Context = context;
        }

        CodeFixContext Context { get; }

        protected override IEnumerable<IntroduceChoiceCodeFix> DefaultVisit(ISymbol symbol) {
            yield break;
        }

        public override IEnumerable<IntroduceChoiceCodeFix> VisitNodeReferenceSymbol(INodeReferenceSymbol nodeReferenceSymbol) {
            yield return new IntroduceChoiceCodeFix(nodeReferenceSymbol, Context);
        }

    }

}