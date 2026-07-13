using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

/// <summary>
/// Eine Transition im Transitionsblock einer Task-Definition, z.B.
/// <c>View --&gt; Ziel on Trigger if Bedingung do Anweisung;</c> — verdrahtet einen deklarierten
/// Quellknoten über eine Kante (<see cref="EdgeSyntax"/>) mit einem Zielknoten; optional folgen
/// Continuation-Anhang, Trigger, Bedingung und <c>do</c>-Klausel. Welche semantische Transition
/// entsteht, bestimmt der Quellknoten: <c>init</c> → <see cref="IInitTransition"/>, Choice-Knoten →
/// <see cref="IChoiceTransition"/>, GUI-Knoten (View/Dialog) → <see cref="ITriggerTransition"/>;
/// Task-Knoten werden stattdessen über <see cref="ExitTransitionDefinitionSyntax"/> weiterverdrahtet.
/// Bis auf <see cref="SourceNode"/> sind alle Bestandteile fehlertolerant optional (<c>null</c>).
/// </summary>
[Serializable]
[SampleSyntax("Node --> Target on Trigger if Condition do Instruction;")]
public partial class TransitionDefinitionSyntax: SyntaxNode {

    internal TransitionDefinitionSyntax(TextExtent extent,
                                        SourceNodeSyntax sourceNode,
                                        EdgeSyntax? edgeSyntax,
                                        TargetNodeSyntax? targetNode,
                                        ContinuationTransitionSyntax? continuationTransition,
                                        TriggerSyntax? trigger,
                                        ConditionClauseSyntax? conditionClause,
                                        DoClauseSyntax? doClause): base(extent) {

        AddChildNode(SourceNode             = sourceNode);
        AddChildNode(Edge                   = edgeSyntax);
        AddChildNode(TargetNode             = targetNode);
        AddChildNode(ContinuationTransition = continuationTransition);
        AddChildNode(Trigger                = trigger);
        AddChildNode(ConditionClause        = conditionClause);
        AddChildNode(DoClause               = doClause);
    }

    /// <summary>Der Quellknoten der Transition (<c>init</c> oder ein benannter Knoten) — immer vorhanden.</summary>
    public SourceNodeSyntax SourceNode { get; }

    /// <summary>Die Kante (<c>--&gt;</c>/<c>o-&gt;</c>/<c>==&gt;</c>) — <c>null</c>, wenn sie im Quelltext fehlt.</summary>
    public EdgeSyntax? Edge { get; }

    /// <summary>Der Zielknoten — <c>null</c>, wenn er im Quelltext fehlt.</summary>
    public TargetNodeSyntax? TargetNode { get; }

    /// <summary>
    /// Der optionale Fortsetzungs-Anhang (<c>o-^</c>/<c>--^</c> Task, ab Sprachversion 2) hinter dem
    /// Zielknoten — <c>null</c>, wenn nicht angegeben.
    /// </summary>
    public ContinuationTransitionSyntax? ContinuationTransition { get; }

    /// <summary>
    /// Der optionale Auslöser (<c>on Signal</c> bzw. <c>spontaneous</c>/<c>spont</c>) — <c>null</c>,
    /// wenn nicht angegeben.
    /// </summary>
    public TriggerSyntax? Trigger { get; }

    /// <summary>Die optionale Bedingung (<c>if</c>/<c>else</c>/<c>else if</c>) — <c>null</c>, wenn nicht angegeben.</summary>
    public ConditionClauseSyntax? ConditionClause { get; }

    /// <summary>Die optionale <c>do</c>-Klausel — <c>null</c>, wenn nicht angegeben.</summary>
    public DoClauseSyntax? DoClause { get; }

    /// <summary>
    /// Das abschließende <c>;</c> — ein Missing-Token (<see cref="SyntaxToken.IsMissing"/>), wenn es fehlt.
    /// </summary>
    public SyntaxToken Semicolon => ChildTokens().FirstOrMissing(SyntaxTokenType.Semicolon);

}