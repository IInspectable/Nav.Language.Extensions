#nullable enable

using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

[Serializable]
public abstract class SourceNodeSyntax: SyntaxNode {

    protected SourceNodeSyntax(TextExtent extent): base(extent) {
    }

    public abstract string Name { get; }

}

[Serializable]
[SampleSyntax("init")]
public partial class InitSourceNodeSyntax: SourceNodeSyntax {

    internal InitSourceNodeSyntax(TextExtent extent): base(extent) {
    }

    public SyntaxToken InitKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.InitKeyword);

    public override string Name => InitKeyword.ToString();

}

[Serializable]
[SampleSyntax("Identifier")]
public partial class IdentifierSourceNodeSyntax: SourceNodeSyntax {

    internal IdentifierSourceNodeSyntax(TextExtent extent): base(extent) {
    }

    public SyntaxToken Identifier => ChildTokens().FirstOrMissing(SyntaxTokenType.Identifier);

    public override string Name => Identifier.ToString();

}