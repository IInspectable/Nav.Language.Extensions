using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

[Serializable]
[SampleSyntax("[generateto \"StringLiteral\"]")]
public partial class CodeGenerateToDeclarationSyntax: CodeSyntax {

    internal CodeGenerateToDeclarationSyntax(TextExtent extent): base(extent) {
    }

    public SyntaxToken GeneratetoKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.GeneratetoKeyword);
    public SyntaxToken StringLiteral     => ChildTokens().FirstOrMissing(SyntaxTokenType.StringLiteral);

}