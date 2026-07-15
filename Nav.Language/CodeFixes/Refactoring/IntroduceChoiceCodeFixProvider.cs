#region Using Directives

using System.Collections.Generic;
using System.Linq;
using System.Threading;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes.Refactoring; 

/// <summary>
/// Ermittelt die an einer Position/Auswahl anwendbaren <see cref="IntroduceChoiceCodeFix"/>-Fixes
/// (Roslyn-Analogon: <c>CodeFixProvider</c>). Prüft die im <see cref="CodeFixContext"/> gefundenen
/// Symbole und bietet für jede Knotenreferenz, an der sich eine Choice einführen lässt, einen Fix an.
/// </summary>
public static class IntroduceChoiceCodeFixProvider {

    /// <summary>
    /// Liefert alle anwendbaren <see cref="IntroduceChoiceCodeFix"/>-Fixes zu den Symbolen des
    /// <paramref name="context"/> — je Knotenreferenz einen, gefiltert auf jene, die
    /// <see cref="IntroduceChoiceCodeFix.CanApplyFix"/> erfüllen.
    /// </summary>
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