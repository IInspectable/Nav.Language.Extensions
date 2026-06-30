#region Using Directives

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.Internal;

static class SyntaxTokenFactory {

    public static SyntaxToken CreateToken(TextExtent extend, SyntaxTokenType type,
                                          TextClassification classification,
                                          SyntaxNode parent,
                                          SyntaxTriviaList leadingTrivia = default,
                                          SyntaxTriviaList trailingTrivia = default) {

        if (extend.IsMissing) {
            return SyntaxToken.Missing;
        }

        var token = new SyntaxToken(parent, type, classification, extend, leadingTrivia, trailingTrivia);

        return token;
    }

}