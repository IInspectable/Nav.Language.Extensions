#nullable enable

namespace Pharmatechnik.Nav.Language;

sealed class ChoiceTransition: Transition, IChoiceTransition {

    public ChoiceTransition(TransitionDefinitionSyntax syntax,
                            ITaskDefinitionSymbol containingTask,
                            ChoiceNodeReferenceSymbol? choiceReference,
                            EdgeModeSymbol? edgeMode,
                            NodeReferenceSymbol? targetReference)
        : base(syntax, containingTask, choiceReference, edgeMode, targetReference) {
    }

    public IChoiceNodeReferenceSymbol? ChoiceNodeSourceReference => (IChoiceNodeReferenceSymbol?) SourceReference;

}
