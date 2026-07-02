#region Using Directives

using System;
using System.Collections.Generic;

using JetBrains.Annotations;

#endregion

namespace Pharmatechnik.Nav.Language; 

abstract class Transition: ITransition {

    internal Transition(TransitionDefinitionSyntax syntax,
                        ITaskDefinitionSymbol containingTask,
                        NodeReferenceSymbol sourceReference,
                        EdgeModeSymbol edgeMode,
                        NodeReferenceSymbol targetReference) {

        ContainingTask  = containingTask ?? throw new ArgumentNullException(nameof(containingTask));
        Syntax          = syntax         ?? throw new ArgumentNullException(nameof(syntax));
        SourceReference = sourceReference;
        EdgeMode        = edgeMode;
        TargetReference = targetReference;

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

    [NotNull]
    public ITaskDefinitionSymbol ContainingTask { get; }

    [NotNull]
    public Location Location => Syntax.GetLocation();

    [NotNull]
    public TransitionDefinitionSyntax Syntax { get; }

    [CanBeNull]
    public INodeReferenceSymbol SourceReference { get; }

    [CanBeNull]
    public IEdgeModeSymbol EdgeMode { get; }

    [CanBeNull]
    public INodeReferenceSymbol TargetReference { get; }

    [NotNull]
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
    }

}