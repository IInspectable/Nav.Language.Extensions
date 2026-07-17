#region Using Directives

using System;
using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Gemeinsame Basis der Semantic-Model-Umsetzungen von <see cref="ITransition"/>
/// (<see cref="InitTransition"/>, <see cref="ChoiceTransition"/>, <see cref="TriggerTransition"/>).
/// Die Quellknoten-Fallunterscheidung trifft der <see cref="TaskDefinitionSymbolBuilder"/>;
/// <see cref="ExitTransition"/> steht bewusst außerhalb dieser Hierarchie (eigene Syntax, kein
/// <see cref="ITransition"/>).
/// </summary>
abstract class Transition: ITransition {

    /// <summary>
    /// Erzeugt die Transition und verankert sich als <c>Edge</c> ihrer nicht-null Referenzen
    /// (Quelle, Kantenmodus, Ziel).
    /// </summary>
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

    /// <summary>
    /// Liefert die zur Transition gehörenden Teil-Symbole (Quelle, Kantenmodus, Ziel), soweit
    /// vorhanden — die Symbole eines Continuation-Anhangs eingeschlossen.
    /// </summary>
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
