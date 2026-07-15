#region Using Directives

using System;
using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Semantic-Model-Umsetzung von <see cref="IContinuationTransition"/>: der Continuation-Anhang
/// <c>… o-^ Task</c> / <c>… --^ Task</c> (ab Sprachversion 2). Quelle ist der tragende GUI-Knoten der
/// umgebenden Transition, Ziel der Folge-Task; <see cref="EdgeMode"/> kodiert den Fortsetzungs-Kantenmodus
/// (<c>o-^</c> → Modal, <c>--^</c> → Goto).
/// </summary>
sealed class ContinuationTransition: IContinuationTransition {

    /// <summary>
    /// Erzeugt die Continuation und verankert sich als <c>Edge</c> ihrer nicht-null Referenzen
    /// (Quelle, Kantenmodus, Ziel).
    /// </summary>
    /// <param name="syntax">Der zugehörige Syntaxknoten (<c>o-^ Task</c> / <c>--^ Task</c>).</param>
    /// <param name="containingTask">Der Task, in dem die Continuation deklariert ist.</param>
    /// <param name="sourceReference">Referenz auf den tragenden GUI-Knoten (Zielknoten der umgebenden Transition); optional.</param>
    /// <param name="edgeMode">Der Fortsetzungs-Kantenmodus (<c>o-^</c>/<c>--^</c>); optional.</param>
    /// <param name="targetReference">Referenz auf den Folge-Task; optional.</param>
    internal ContinuationTransition(ContinuationTransitionSyntax syntax,
                                    ITaskDefinitionSymbol containingTask,
                                    NodeReferenceSymbol? sourceReference,
                                    EdgeModeSymbol? edgeMode,
                                    NodeReferenceSymbol? targetReference) {

        Syntax          = syntax         ?? throw new ArgumentNullException(nameof(syntax));
        ContainingTask  = containingTask ?? throw new ArgumentNullException(nameof(containingTask));
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

    public ContinuationTransitionSyntax Syntax { get; }

    public ITaskDefinitionSymbol ContainingTask { get; }

    public Location Location => Syntax.GetLocation();

    public INodeReferenceSymbol? SourceReference { get; }

    public IEdgeModeSymbol? EdgeMode { get; }

    public INodeReferenceSymbol? TargetReference { get; }

    /// <summary>
    /// Liefert die zur Continuation gehörenden Teil-Symbole (Quelle, Kantenmodus, Ziel), soweit vorhanden.
    /// </summary>
    public IEnumerable<ISymbol> Symbols() {

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
