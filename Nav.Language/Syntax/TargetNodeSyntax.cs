#nullable enable

using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

[Serializable]
public abstract class TargetNodeSyntax: SyntaxNode {

    protected TargetNodeSyntax(TextExtent extent): base(extent) {
    }

    public abstract string Name { get; }

}

[Serializable]
[SampleSyntax("end")]
public partial class EndTargetNodeSyntax: TargetNodeSyntax {

    internal EndTargetNodeSyntax(TextExtent extent): base(extent) {
    }

    public          SyntaxToken EndKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.EndKeyword);
    public override string      Name       => EndKeyword.ToString();

}

[Serializable]
[SampleSyntax("Identifier (identifierOrStringList)")]
public partial class IdentifierTargetNodeSyntax: TargetNodeSyntax {

    internal IdentifierTargetNodeSyntax(TextExtent extent)
        : base(extent) {
    }

    public          SyntaxToken Identifier => ChildTokens().FirstOrMissing(SyntaxTokenType.Identifier);
    public override string      Name       => Identifier.ToString();

}