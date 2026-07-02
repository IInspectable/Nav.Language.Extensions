#region Using Directives

using System;
using System.Collections.Generic;

using JetBrains.Annotations;

#endregion

namespace Pharmatechnik.Nav.Language; 

sealed class ExitTransition: IExitTransition {

    internal ExitTransition(ExitTransitionDefinitionSyntax syntax,
                            TaskDefinitionSymbol containingTask,
                            [CanBeNull] TaskNodeReferenceSymbol taskNodeReference,
                            [CanBeNull] ExitConnectionPointReferenceSymbol exitConnectionPointReference,
                            [CanBeNull] EdgeModeSymbol edgeMode,
                            [CanBeNull] NodeReferenceSymbol targetReference) {

        Syntax                       = syntax         ?? throw new ArgumentNullException(nameof(syntax));
        ContainingTask               = containingTask ?? throw new ArgumentNullException(nameof(containingTask));
        TaskNodeSourceReference      = taskNodeReference;
        ExitConnectionPointReference = exitConnectionPointReference;
        EdgeMode                     = edgeMode;
        TargetReference              = targetReference;

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

    [NotNull]
    public ITaskDefinitionSymbol ContainingTask { get; }

    [NotNull]
    public Location Location => Syntax.GetLocation();

    [NotNull]
    public ExitTransitionDefinitionSyntax Syntax { get; }

    [CanBeNull]
    public INodeReferenceSymbol SourceReference => TaskNodeSourceReference;

    [CanBeNull]
    public ITaskNodeReferenceSymbol TaskNodeSourceReference { get; }

    [CanBeNull]
    public IExitConnectionPointReferenceSymbol ExitConnectionPointReference { get; }

    [CanBeNull]
    public IEdgeModeSymbol EdgeMode { get; }

    [CanBeNull]
    public INodeReferenceSymbol TargetReference { get; }

    [NotNull]
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
    }

}