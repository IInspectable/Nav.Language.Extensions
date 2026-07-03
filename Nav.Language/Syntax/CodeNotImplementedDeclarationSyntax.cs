using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

[Serializable]
[SampleSyntax("[notimplemented]")]
public partial class CodeNotImplementedDeclarationSyntax: CodeSyntax {

    internal CodeNotImplementedDeclarationSyntax(TextExtent extent): base(extent) {
    }

    public SyntaxToken NotimplementedKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.NotimplementedKeyword);

}