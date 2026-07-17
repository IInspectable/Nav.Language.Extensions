namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Semantic-Model-Umsetzung von <see cref="IInitTransition"/> — die Transition mit Init-Knoten als
/// Quelle, z.B. <c>init --&gt; Start;</c>.
/// </summary>
sealed class InitTransition: Transition, IInitTransition {

    public InitTransition(TransitionDefinitionSyntax syntax,
                          ITaskDefinitionSymbol containingTask,
                          InitNodeReferenceSymbol? initNodeReference,
                          EdgeModeSymbol? edgeMode,
                          NodeReferenceSymbol? targetReference,
                          ContinuationTransition? continuationTransition)
        : base(syntax, containingTask, initNodeReference, edgeMode, targetReference, continuationTransition) {
    }

    /// <inheritdoc/>
    public IInitNodeReferenceSymbol? InitNodeSourceReference => (IInitNodeReferenceSymbol?) SourceReference;

}
