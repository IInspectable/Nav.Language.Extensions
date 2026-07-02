#nullable enable

using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

[Serializable]
public abstract class ConditionClauseSyntax: SyntaxNode {

    protected ConditionClauseSyntax(TextExtent extent): base(extent) {
    }

}

[Serializable]
[SampleSyntax("if Condition")]
public partial class IfConditionClauseSyntax: ConditionClauseSyntax {

    internal IfConditionClauseSyntax(TextExtent extent, IdentifierOrStringSyntax? identifierOrString): base(extent) {
        AddChildNode(IdentifierOrString = identifierOrString);
    }

    public SyntaxToken IfKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.IfKeyword);

    public IdentifierOrStringSyntax? IdentifierOrString { get; }

}

[Serializable]
[SampleSyntax("else")]
public partial class ElseConditionClauseSyntax: ConditionClauseSyntax {

    internal ElseConditionClauseSyntax(TextExtent extent): base(extent) {
    }

    public SyntaxToken ElseKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.ElseKeyword);

}

[Serializable]
[SampleSyntax("else if Condition")]
public partial class ElseIfConditionClauseSyntax: ConditionClauseSyntax {

    internal ElseIfConditionClauseSyntax(TextExtent extent, ElseConditionClauseSyntax elseCondition, IfConditionClauseSyntax ifCondition): base(extent) {
        AddChildNode(ElseCondition = elseCondition);
        AddChildNode(IfCondition   = ifCondition);
    }

    public ElseConditionClauseSyntax ElseCondition { get; }

    public IfConditionClauseSyntax IfCondition { get; }

}