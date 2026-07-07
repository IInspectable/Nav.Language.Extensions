namespace Pharmatechnik.Nav.Language;

sealed class InitTransition: Transition, IInitTransition {

    public InitTransition(TransitionDefinitionSyntax syntax,
                          ITaskDefinitionSymbol containingTask,
                          InitNodeReferenceSymbol? initNodeReference,
                          EdgeModeSymbol? edgeMode,
                          NodeReferenceSymbol? targetReference,
                          ContinuationTransition? continuationTransition)
        : base(syntax, containingTask, initNodeReference, edgeMode, targetReference, continuationTransition) {
    }

    public IInitNodeReferenceSymbol? InitNodeSourceReference => (IInitNodeReferenceSymbol?) SourceReference;

}
