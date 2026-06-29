#region Using Directives

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.Internal;

static class SyntaxTokenFactory {

    public static SyntaxToken CreateToken(TextExtent extend, SyntaxTokenType type, TextClassification classification, SyntaxNode parent) {

        if (extend.IsMissing) {
            return SyntaxToken.Missing;
        }

        var token = new SyntaxToken(parent, type, classification, extend);

        return token;
    }
}
