#nullable enable

using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

[Serializable]
[SampleSyntax("choice ChoiceName;")]
public partial class ChoiceNodeDeclarationSyntax: NodeDeclarationSyntax {

    internal ChoiceNodeDeclarationSyntax(TextExtent extent)
        : base(extent) {
    }

    public SyntaxToken ChoiceKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.ChoiceKeyword);
    public SyntaxToken Identifier    => ChildTokens().FirstOrMissing(SyntaxTokenType.Identifier);

}