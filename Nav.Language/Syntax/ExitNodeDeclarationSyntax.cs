#nullable enable

using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

[Serializable]
[SampleSyntax("exit Identifier;")]
public partial class ExitNodeDeclarationSyntax: ConnectionPointNodeSyntax {

    internal ExitNodeDeclarationSyntax(TextExtent extent)
        : base(extent) {
    }

    public SyntaxToken ExitKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.ExitKeyword);
    public SyntaxToken Identifier  => ChildTokens().FirstOrMissing(SyntaxTokenType.Identifier);

}