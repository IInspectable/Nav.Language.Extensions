using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

[Serializable]
[SampleSyntax("[donotinject]")]
public partial class CodeDoNotInjectDeclarationSyntax: CodeSyntax {

    internal CodeDoNotInjectDeclarationSyntax(TextExtent extent): base(extent) {
    }

    public SyntaxToken DonotinjectKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.DonotinjectKeyword);

}