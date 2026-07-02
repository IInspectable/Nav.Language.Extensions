#nullable enable

using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

[Serializable]
[SampleSyntax("[result Type p]")]
public partial class CodeResultDeclarationSyntax: CodeSyntax {

    internal CodeResultDeclarationSyntax(TextExtent extent, ParameterSyntax result): base(extent) {
        AddChildNode(Result = result);
    }

    public SyntaxToken ResultKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.ResultKeyword);

    public ParameterSyntax Result { get; }

}