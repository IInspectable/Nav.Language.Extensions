#nullable enable

using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

[Serializable]
[SampleSyntax("[abstractmethod]")]
public partial class CodeAbstractMethodDeclarationSyntax: CodeSyntax {

    internal CodeAbstractMethodDeclarationSyntax(TextExtent extent): base(extent) {
    }

    public SyntaxToken AbstractmethodKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.AbstractmethodKeyword);

}