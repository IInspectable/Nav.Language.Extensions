#region Using Directives

using System;
using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Semantic-Model-Umsetzung von <see cref="IExitTransition"/>: verdrahtet einen benannten
/// Exit-Verbindungspunkt eines Task-Knotens mit einem Zielknoten, z.B.
/// <c>TaskKnoten:Abbrechen --&gt; end;</c>. Steht bewusst außerhalb der
/// <see cref="Transition"/>-Hierarchie (eigene Syntax, kein <see cref="ITransition"/>).
/// </summary>
sealed class ExitTransition: IExitTransition {

    /// <summary>
    /// Erzeugt die Exit-Transition und verankert sich als <c>Edge</c> ihrer nicht-null Referenzen
    /// (Quelle, Kantenmodus, Ziel) sowie als
    /// <see cref="ExitConnectionPointReferenceSymbol.ExitTransition"/> der
    /// Exit-Verbindungspunkt-Referenz.
    /// </summary>
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

    /// <summary>
    /// Die Quellreferenz — stets identisch mit <see cref="TaskNodeSourceReference"/>, denn die
    /// Quelle einer Exit-Transition ist immer ein Task-Knoten.
    /// </summary>
    public INodeReferenceSymbol? SourceReference => TaskNodeSourceReference;

    public ITaskNodeReferenceSymbol? TaskNodeSourceReference { get; }

    public IExitConnectionPointReferenceSymbol? ExitConnectionPointReference { get; }

    public IEdgeModeSymbol? EdgeMode { get; }

    public INodeReferenceSymbol? TargetReference { get; }

    public IContinuationTransition? ContinuationTransition { get; }

    /// <summary>
    /// Liefert die zur Exit-Transition gehörenden Teil-Symbole (Quelle,
    /// Exit-Verbindungspunkt-Referenz, Kantenmodus, Ziel), soweit vorhanden — die Symbole eines
    /// Continuation-Anhangs eingeschlossen.
    /// </summary>
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
