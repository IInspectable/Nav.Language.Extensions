namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Semantic-Model-Umsetzung von <see cref="IChoiceTransition"/> — die Transition mit Choice-Knoten
/// als Quelle, z.B. <c>Auswahl --&gt; Ziel if Bedingung;</c>.
/// </summary>
sealed class ChoiceTransition: Transition, IChoiceTransition {

    public ChoiceTransition(TransitionDefinitionSyntax syntax,
                            ITaskDefinitionSymbol containingTask,
                            ChoiceNodeReferenceSymbol? choiceReference,
                            EdgeModeSymbol? edgeMode,
                            NodeReferenceSymbol? targetReference,
                            ContinuationTransition? continuationTransition)
        : base(syntax, containingTask, choiceReference, edgeMode, targetReference, continuationTransition) {
    }

    /// <inheritdoc/>
    public IChoiceNodeReferenceSymbol? ChoiceNodeSourceReference => (IChoiceNodeReferenceSymbol?) SourceReference;

}
