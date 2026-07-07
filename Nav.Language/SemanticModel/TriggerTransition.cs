using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language;

sealed class TriggerTransition: Transition, ITriggerTransition {

    public TriggerTransition(TransitionDefinitionSyntax syntax,
                             ITaskDefinitionSymbol containingTask,
                             GuiNodeReferenceSymbol? sourceReference,
                             EdgeModeSymbol? edgeMode,
                             NodeReferenceSymbol? targetReference,
                             ContinuationTransition? continuationTransition,
                             SymbolCollection<TriggerSymbol>? triggers)
        : base(syntax, containingTask, sourceReference, edgeMode, targetReference, continuationTransition) {

        Triggers = triggers ?? new SymbolCollection<TriggerSymbol>();

        foreach (var trigger in Triggers) {
            trigger.Transition = this;
        }
    }

    public IGuiNodeReferenceSymbol? GuiNodeSourceReference => (IGuiNodeReferenceSymbol?) SourceReference;

    IReadOnlySymbolCollection<ITriggerSymbol> ITriggerTransition.Triggers => Triggers;

    public SymbolCollection<TriggerSymbol> Triggers { get; }

    public override IEnumerable<ISymbol> Symbols() {

        foreach (var symbol in base.Symbols()) {
            yield return symbol;
        }

        foreach (var trigger in Triggers) {
            yield return trigger;
        }
    }

}
