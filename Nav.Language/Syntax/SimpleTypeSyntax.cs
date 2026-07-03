using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

[Serializable]
[SampleSyntax("int?")]
public partial class SimpleTypeSyntax: CodeTypeSyntax {

    internal SimpleTypeSyntax(TextExtent extent): base(extent) {
    }

    public SyntaxToken Identifier   => ChildTokens().FirstOrMissing(SyntaxTokenType.Identifier);
    public SyntaxToken Questionmark => ChildTokens().FirstOrMissing(SyntaxTokenType.Questionmark);

}