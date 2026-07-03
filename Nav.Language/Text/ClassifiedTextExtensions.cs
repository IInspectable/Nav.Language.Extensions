#region Using Directives

using System.Linq;
using System.Text;
using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language.Text;

public static class ClassifiedTextExtensions {

    public static string JoinText(this IEnumerable<ClassifiedText> parts) {
        return parts.Aggregate(new StringBuilder(), (sb, p) => sb.Append(p.Text), sb => sb.ToString());
    }

    public static int Length(this IEnumerable<ClassifiedText> parts) {
        return parts.Aggregate(0, (acc, ct) => acc + ct.Text.Length);
    }

    public static IEnumerable<ClassifiedText> GetClassifiedText(this SyntaxTree syntaxTree, TextExtent extent) {

        var source = syntaxTree.SourceText;

        // Klassifizierte Stücke in Quelltext-Reihenfolge: je signifikantem bzw. Trenner-Token seine
        // Leading-Trivia, der Token selbst und seine Trailing-Trivia (Roslyn-Modell). Die flachen Trivia-Token
        // werden übersprungen — ihr Text und ihre Klassifikation kommen über die angehängte Trivia. So bleibt
        // das Ergebnis identisch, auch wenn die Trivia nicht mehr im flachen Token-Strom geführt wird.
        foreach (var token in syntaxTree.Tokens) {

            if (SyntaxFacts.IsTrivia(token.Type)) {
                continue;
            }

            foreach (var trivia in token.LeadingTrivia) {
                if (TryClip(trivia.Extent, extent, out var clip)) {
                    yield return new ClassifiedText(source.Substring(clip), ClassificationOf(trivia.Type));
                }
            }

            if (TryClip(token.Extent, extent, out var tokenClip)) {
                yield return new ClassifiedText(source.Substring(tokenClip), token.Classification);
            }

            foreach (var trivia in token.TrailingTrivia) {
                if (TryClip(trivia.Extent, extent, out var clip)) {
                    yield return new ClassifiedText(source.Substring(clip), ClassificationOf(trivia.Type));
                }
            }
        }
    }

    /// <summary>Schnittmenge aus Stück- und Fenster-Ausschnitt (Überlappung wie <c>includeOverlapping: true</c>).</summary>
    static bool TryClip(TextExtent piece, TextExtent window, out TextExtent clipped) {

        var start = piece.Start > window.Start ? piece.Start : window.Start;
        var end   = piece.End   < window.End   ? piece.End   : window.End;

        if (start < end) {
            clipped = TextExtent.FromBounds(start, end);
            return true;
        }

        clipped = default;
        return false;
    }

    static TextClassification ClassificationOf(SyntaxTokenType triviaType) {
        if (triviaType == SyntaxTokenType.SingleLineComment || triviaType == SyntaxTokenType.MultiLineComment) {
            return TextClassification.Comment;
        }

        // Übersprungene Läufe (strukturierte Skip-Trivia) behalten ihre Fehler-Klassifikation — wie zuvor,
        // als die übersprungenen Token noch als Skiped-Token im flachen Strom standen.
        if (triviaType == SyntaxTokenType.SkippedTokensTrivia) {
            return TextClassification.Skiped;
        }

        return TextClassification.Whitespace;
    }

}