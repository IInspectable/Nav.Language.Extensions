#region Using Directives

using System;
using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language;

abstract class Transition: ITransition {

    internal Transition(TransitionDefinitionSyntax syntax,
                        ITaskDefinitionSymbol containingTask,
                        NodeReferenceSymbol? sourceReference,
                        EdgeModeSymbol? edgeMode,
                        NodeReferenceSymbol? targetReference,
                        ContinuationTransition? continuationTransition) {

        ContainingTask         = containingTask ?? throw new ArgumentNullException(nameof(containingTask));
        Syntax                 = syntax         ?? throw new ArgumentNullException(nameof(syntax));
        SourceReference        = sourceReference;
        EdgeMode               = edgeMode;
        TargetReference        = targetReference;
        ContinuationTransition = continuationTransition;

        if (sourceReference != null) {
            sourceReference.Edge = this;
        }

        if (edgeMode != null) {
            edgeMode.Edge = this;
        }

        if (targetReference != null) {
            targetReference.Edge = this;
        }

    }

    public ITaskDefinitionSymbol ContainingTask { get; }

    public Location Location => Syntax.GetLocation();

    public TransitionDefinitionSyntax Syntax { get; }

    public INodeReferenceSymbol? SourceReference { get; }

    public IEdgeModeSymbol? EdgeMode { get; }

    public INodeReferenceSymbol? TargetReference { get; }

    public IContinuationTransition? ContinuationTransition { get; }

    public virtual IEnumerable<ISymbol> Symbols() {

        if (SourceReference != null) {
            yield return SourceReference;
        }

        if (EdgeMode != null) {
            yield return EdgeMode;
        }

        if (TargetReference != null) {
            yield return TargetReference;
        }

        if (ContinuationTransition != null) {
            foreach (var symbol in ContinuationTransition.Symbols()) {
                yield return symbol;
            }
        }
    }

}
