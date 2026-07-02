#nullable enable

using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

[Serializable]
[SampleSyntax("end;")]
public partial class EndNodeDeclarationSyntax: ConnectionPointNodeSyntax {

    internal EndNodeDeclarationSyntax(TextExtent extent)
        : base(extent) {
    }

    public SyntaxToken EndKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.EndKeyword);

}