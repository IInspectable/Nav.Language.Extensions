#nullable enable

using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

[Serializable]
[SampleSyntax("view Identifier;")]
public partial class ViewNodeDeclarationSyntax: NodeDeclarationSyntax {

    internal ViewNodeDeclarationSyntax(TextExtent extent)
        : base(extent) {
    }

    public SyntaxToken ViewKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.ViewKeyword);
    public SyntaxToken Identifier  => ChildTokens().FirstOrMissing(SyntaxTokenType.Identifier);

}