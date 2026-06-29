#region Using Directives

using System.Collections.Immutable;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.Internal;

static class SyntaxTokenFactory {

    public static SyntaxToken CreateToken(TextExtent extend, SyntaxTokenType type,
                                          TextClassification classification,
                                          SyntaxNode parent,
                                          ImmutableArray<SyntaxTrivia> leadingTrivia = default,
                                          ImmutableArray<SyntaxTrivia> trailingTrivia = default) {

        if (extend.IsMissing) {
            return SyntaxToken.Missing;
        }

        var token = new SyntaxToken(parent, type, classification, extend, leadingTrivia, trailingTrivia);

        return token;
    }

}