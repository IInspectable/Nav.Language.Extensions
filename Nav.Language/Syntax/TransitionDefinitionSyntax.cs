using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

[Serializable]
[SampleSyntax("Node --> Target on Trigger if Condition do Instruction;")]
public partial class TransitionDefinitionSyntax: SyntaxNode {

    internal TransitionDefinitionSyntax(TextExtent extent,
                                        SourceNodeSyntax sourceNode,
                                        EdgeSyntax? edgeSyntax,
                                        TargetNodeSyntax? targetNode,
                                        TriggerSyntax? trigger,
                                        ConditionClauseSyntax? conditionClause,
                                        DoClauseSyntax? doClause): base(extent) {

        AddChildNode(SourceNode      = sourceNode);
        AddChildNode(Edge            = edgeSyntax);
        AddChildNode(TargetNode      = targetNode);
        AddChildNode(Trigger         = trigger);
        AddChildNode(ConditionClause = conditionClause);
        AddChildNode(DoClause        = doClause);
    }

    public SourceNodeSyntax SourceNode { get; }

    public EdgeSyntax? Edge { get; }

    public TargetNodeSyntax? TargetNode { get; }

    public TriggerSyntax? Trigger { get; }

    public ConditionClauseSyntax? ConditionClause { get; }

    public DoClauseSyntax? DoClause { get; }

    public SyntaxToken Semicolon => ChildTokens().FirstOrMissing(SyntaxTokenType.Semicolon);

}