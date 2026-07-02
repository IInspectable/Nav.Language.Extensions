#nullable enable

using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

[Serializable]
public abstract class NodeDeclarationSyntax: SyntaxNode {

    protected NodeDeclarationSyntax(TextExtent extent): base(extent) {
    }

    public SyntaxToken Semicolon => ChildTokens().FirstOrMissing(SyntaxTokenType.Semicolon);

}