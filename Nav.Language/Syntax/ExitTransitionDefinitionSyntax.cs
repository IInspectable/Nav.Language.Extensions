using System;

using Pharmatechnik.Nav.Language.Internal;
using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Eine Exit-Transition, z.B. <c>TaskKnoten:Abbrechen --&gt; end;</c> — verdrahtet einen benannten
/// Exit-Verbindungspunkt eines eingebetteten Task-Knotens mit einem Zielknoten. Semantisch muss der
/// Quellknoten ein Task-Knoten sein (→ <see cref="IExitTransition"/>); Task-Knoten haben keine
/// eigenen ausgehenden <see cref="TransitionDefinitionSyntax"/>-Kanten, ihre Fortsetzung läuft
/// ausschließlich über ihre Exits. Anders als eine Transition trägt sie keinen Trigger; Bedingung,
/// <c>do</c>-Klausel und Continuation-Anhang sind wie dort optional.
/// </summary>
[Serializable]
[SampleSyntax("SourceNode:ExitIdentifier --> TargetNode if Condition do Instruction;")]
public partial class ExitTransitionDefinitionSyntax: SyntaxNode {

    internal ExitTransitionDefinitionSyntax(TextExtent extent,
                                            IdentifierSourceNodeSyntax sourceNode,
                                            EdgeSyntax? edge,
                                            TargetNodeSyntax? targetNode,
                                            ContinuationTransitionSyntax? continuationTransition,
                                            ConditionClauseSyntax? conditionClause,
                                            DoClauseSyntax? doClause): base(extent) {

        AddChildNode(SourceNode             = sourceNode);
        AddChildNode(Edge                   = edge);
        AddChildNode(TargetNode             = targetNode);
        AddChildNode(ContinuationTransition = continuationTransition);
        AddChildNode(ConditionClause        = conditionClause);
        AddChildNode(DoClause               = doClause);
    }

    /// <summary>Der Quellknoten — der Name des Task-Knotens, dessen Exit verdrahtet wird; immer vorhanden.</summary>
    public IdentifierSourceNodeSyntax SourceNode { get; }

    /// <summary>Der <c>:</c> zwischen Task-Knoten und Exit-Namen — ein Missing-Token, wenn er fehlt.</summary>
    public SyntaxToken Colon => ChildTokens().FirstOrMissing(SyntaxTokenType.Colon);

    /// <summary>
    /// Der Name des Exit-Verbindungspunkts hinter dem <c>:</c> — ein Missing-Token
    /// (<see cref="SyntaxToken.IsMissing"/>), wenn er fehlt.
    /// </summary>
    [SuppressCodeSanityCheck("Der Name ExitIdentifier ist hier ausdrücklich gewollt.")]
    public SyntaxToken ExitIdentifier => ChildTokens().FirstOrMissing(SyntaxTokenType.Identifier);

    /// <summary>Die Kante (<c>--&gt;</c>/<c>o-&gt;</c>/<c>==&gt;</c>) — <c>null</c>, wenn sie im Quelltext fehlt.</summary>
    public EdgeSyntax? Edge { get; }

    /// <summary>Der Zielknoten — <c>null</c>, wenn er im Quelltext fehlt.</summary>
    public TargetNodeSyntax? TargetNode { get; }

    /// <summary>
    /// Der optionale Fortsetzungs-Anhang (<c>o-^</c>/<c>--^</c> Task, ab Sprachversion 2) hinter dem
    /// Zielknoten — <c>null</c>, wenn nicht angegeben.
    /// </summary>
    public ContinuationTransitionSyntax? ContinuationTransition { get; }

    /// <summary>Die optionale Bedingung (<c>if</c>/<c>else</c>/<c>else if</c>) — <c>null</c>, wenn nicht angegeben.</summary>
    public ConditionClauseSyntax? ConditionClause { get; }

    /// <summary>Die optionale <c>do</c>-Klausel — <c>null</c>, wenn nicht angegeben.</summary>
    public DoClauseSyntax? DoClause { get; }

    /// <summary>
    /// Das abschließende <c>;</c> — ein Missing-Token (<see cref="SyntaxToken.IsMissing"/>), wenn es fehlt.
    /// </summary>
    public SyntaxToken Semicolon => ChildTokens().FirstOrMissing(SyntaxTokenType.Semicolon);

}
