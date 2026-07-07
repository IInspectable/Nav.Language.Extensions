using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

[Serializable]
[SampleSyntax("choice ChoiceName [params T1 p1, T2 p2];")]
public partial class ChoiceNodeDeclarationSyntax: NodeDeclarationSyntax {

    internal ChoiceNodeDeclarationSyntax(TextExtent extent,
                                         CodeParamsDeclarationSyntax? codeParamsDeclaration)
        : base(extent) {

        AddChildNode(CodeParamsDeclaration = codeParamsDeclaration);
    }

    public SyntaxToken ChoiceKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.ChoiceKeyword);
    public SyntaxToken Identifier    => ChildTokens().FirstOrMissing(SyntaxTokenType.Identifier);

    public CodeParamsDeclarationSyntax? CodeParamsDeclaration { get; }

}