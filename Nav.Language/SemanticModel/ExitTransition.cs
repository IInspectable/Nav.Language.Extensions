#region Using Directives

using System;
using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language;

sealed class ExitTransition: IExitTransition {

    internal ExitTransition(ExitTransitionDefinitionSyntax syntax,
                            TaskDefinitionSymbol containingTask,
                            TaskNodeReferenceSymbol? taskNodeReference,
                            ExitConnectionPointReferenceSymbol? exitConnectionPointReference,
                            EdgeModeSymbol? edgeMode,
                            NodeReferenceSymbol? targetReference,
                            ContinuationTransition? continuationTransition) {

        Syntax                       = syntax         ?? throw new ArgumentNullException(nameof(syntax));
        ContainingTask               = containingTask ?? throw new ArgumentNullException(nameof(containingTask));
        TaskNodeSourceReference      = taskNodeReference;
        ExitConnectionPointReference = exitConnectionPointReference;
        EdgeMode                     = edgeMode;
        TargetReference              = targetReference;
        ContinuationTransition       = continuationTransition;

        if (taskNodeReference != null) {
            taskNodeReference.Edge = this;
        }

        if (edgeMode != null) {
            edgeMode.Edge = this;
        }

        if (targetReference != null) {
            targetReference.Edge = this;
        }

        if (exitConnectionPointReference != null) {
            exitConnectionPointReference.ExitTransition = this;
        }
    }

    public ITaskDefinitionSymbol ContainingTask { get; }

    public Location Location => Syntax.GetLocation();

    public ExitTransitionDefinitionSyntax Syntax { get; }

    public INodeReferenceSymbol? SourceReference => TaskNodeSourceReference;

    public ITaskNodeReferenceSymbol? TaskNodeSourceReference { get; }

    public IExitConnectionPointReferenceSymbol? ExitConnectionPointReference { get; }

    public IEdgeModeSymbol? EdgeMode { get; }

    public INodeReferenceSymbol? TargetReference { get; }

    public IContinuationTransition? ContinuationTransition { get; }

    public IEnumerable<ISymbol> Symbols() {

        if (SourceReference != null) {
            yield return SourceReference;
        }

        if (ExitConnectionPointReference != null) {
            yield return ExitConnectionPointReference;
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
