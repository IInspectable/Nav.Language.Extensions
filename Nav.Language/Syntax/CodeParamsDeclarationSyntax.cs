#nullable enable

using Pharmatechnik.Nav.Language.Text;

using System;

namespace Pharmatechnik.Nav.Language; 

[Serializable]
[SampleSyntax("[params Type1 p1, Type2 p2]")]
public partial class CodeParamsDeclarationSyntax: CodeSyntax {

    internal CodeParamsDeclarationSyntax(TextExtent extent, ParameterListSyntax? parameterList)
        : base(extent) {
        AddChildNode(ParameterList = parameterList);
    }

    public SyntaxToken ParamsKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.ParamsKeyword);

    public ParameterListSyntax? ParameterList { get; }

}