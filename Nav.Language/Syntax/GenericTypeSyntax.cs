using System;
using System.Collections.Generic;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

[Serializable]
[SampleSyntax("Type<T1, T2<T3, T4>>")]
public partial class GenericTypeSyntax: CodeTypeSyntax {

    internal GenericTypeSyntax(TextExtent extent, IReadOnlyList<CodeTypeSyntax> genericArguments): base(extent) {
        AddChildNodes(GenericArguments = genericArguments);
    }

    public SyntaxToken Identifier => ChildTokens().FirstOrMissing(SyntaxTokenType.Identifier);

    public IReadOnlyList<CodeTypeSyntax> GenericArguments { get; }

}