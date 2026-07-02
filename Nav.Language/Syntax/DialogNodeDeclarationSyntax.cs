#nullable enable

using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

[Serializable]
[SampleSyntax("dialog Identifier;")]
public partial class DialogNodeDeclarationSyntax: NodeDeclarationSyntax {

    internal DialogNodeDeclarationSyntax(TextExtent extent): base(extent) {
    }

    public SyntaxToken DialogKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.DialogKeyword);
    public SyntaxToken Identifier    => ChildTokens().FirstOrMissing(SyntaxTokenType.Identifier);

}