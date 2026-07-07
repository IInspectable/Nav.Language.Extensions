using System;

using Pharmatechnik.Nav.Language.Internal;
using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language;

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

    public IdentifierSourceNodeSyntax SourceNode { get; }

    public SyntaxToken Colon => ChildTokens().FirstOrMissing(SyntaxTokenType.Colon);

    [SuppressCodeSanityCheck("Der Name ExitIdentifier ist hier ausdrücklich gewollt.")]
    public SyntaxToken ExitIdentifier => ChildTokens().FirstOrMissing(SyntaxTokenType.Identifier);

    public EdgeSyntax? Edge { get; }

    public TargetNodeSyntax? TargetNode { get; }

    public ContinuationTransitionSyntax? ContinuationTransition { get; }

    public ConditionClauseSyntax? ConditionClause { get; }

    public DoClauseSyntax? DoClause { get; }

    public SyntaxToken Semicolon => ChildTokens().FirstOrMissing(SyntaxTokenType.Semicolon);

}
