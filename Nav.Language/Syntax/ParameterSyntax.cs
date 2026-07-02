#nullable enable

using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

[Serializable]
[SampleSyntax("Type param")]
public partial class ParameterSyntax: SyntaxNode {

    readonly CodeTypeSyntax _type;

    internal ParameterSyntax(TextExtent extent, CodeTypeSyntax type): base(extent) {
        AddChildNode(_type = type);
    }

    public CodeTypeSyntax Type => _type;

    public SyntaxToken Identifier => ChildTokens().FirstOrMissing(SyntaxTokenType.Identifier);

}