#nullable enable

using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

[Serializable]
[SampleSyntax("[]")]
public partial class ArrayRankSpecifierSyntax: SyntaxNode {

    internal ArrayRankSpecifierSyntax(TextExtent extent): base(extent) {
    }

    public SyntaxToken OpenBracket  => ChildTokens().FirstOrMissing(SyntaxTokenType.OpenBracket);
    public SyntaxToken CloseBracket => ChildTokens().FirstOrMissing(SyntaxTokenType.CloseBracket);

}