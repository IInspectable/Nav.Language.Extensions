namespace Pharmatechnik.Nav.Language;

sealed class ChoiceTransition: Transition, IChoiceTransition {

    public ChoiceTransition(TransitionDefinitionSyntax syntax,
                            ITaskDefinitionSymbol containingTask,
                            ChoiceNodeReferenceSymbol? choiceReference,
                            EdgeModeSymbol? edgeMode,
                            NodeReferenceSymbol? targetReference,
                            ContinuationTransition? continuationTransition)
        : base(syntax, containingTask, choiceReference, edgeMode, targetReference, continuationTransition) {
    }

    public IChoiceNodeReferenceSymbol? ChoiceNodeSourceReference => (IChoiceNodeReferenceSymbol?) SourceReference;

}
