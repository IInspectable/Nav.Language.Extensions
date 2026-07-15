#region Using Directives

using System.Linq;
using System.Text;
using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language.Text;

/// <summary>
/// Erweiterungsmethoden rund um <see cref="ClassifiedText"/>: das Verketten bzw. Vermessen einer
/// Stück-Folge sowie das Klassifizieren eines Quelltext-Ausschnitts über den Token-Strom eines
/// <see cref="SyntaxTree"/> (Grundlage der syntaktischen Färbung in QuickInfo und Hosts).
/// </summary>
public static class ClassifiedTextExtensions {

    /// <summary>Fügt den <see cref="ClassifiedText.Text"/> aller Stücke ohne Trenner zum reinen Anzeigetext zusammen.</summary>
    /// <param name="parts">Die zu verkettenden Stücke.</param>
    /// <returns>Der aneinandergehängte Text.</returns>
    public static string JoinText(this IEnumerable<ClassifiedText> parts) {
        return parts.Aggregate(new StringBuilder(), (sb, p) => sb.Append(p.Text), sb => sb.ToString());
    }

    /// <summary>Die Gesamtlänge in Zeichen — die Summe der <see cref="ClassifiedText.Text"/>-Längen aller Stücke.</summary>
    /// <param name="parts">Die zu vermessenden Stücke.</param>
    /// <returns>Die aufsummierte Zeichenlänge.</returns>
    public static int Length(this IEnumerable<ClassifiedText> parts) {
        return parts.Aggregate(0, (acc, ct) => acc + ct.Text.Length);
    }

    /// <summary>
    /// Zerlegt den mit <paramref name="extent"/> begrenzten Quelltext-Ausschnitt in
    /// <see cref="ClassifiedText"/>-Stücke in Quelltext-Reihenfolge. Läuft den Token-Strom des
    /// <paramref name="syntaxTree"/> ab und liefert je signifikantem Token dessen
    /// Leading-Trivia, den Token selbst und dessen Trailing-Trivia (Roslyn-Modell), jeweils auf
    /// den Extent zugeschnitten (überlappende Stücke werden geclippt).
    /// </summary>
    /// <param name="syntaxTree">Der Syntaxbaum, dessen Token-Strom und Quelltext abgelaufen werden.</param>
    /// <param name="extent">Der Extent, auf den die Stücke beschränkt werden.</param>
    /// <returns>Die klassifizierten Stücke innerhalb des Extents, in Quelltext-Reihenfolge.</returns>
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

    /// <summary>Schnittmenge aus Stück- und Extent-Ausschnitt (Überlappung wie <c>includeOverlapping: true</c>).</summary>
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

    /// <summary>
    /// Bildet den lexikalischen Typ einer Trivia auf ihre <see cref="TextClassification"/> ab:
    /// Kommentare werden zu <see cref="TextClassification.Comment"/>, übersprungene Läufe
    /// (Skip-Trivia) behalten ihre Fehler-Klassifikation <see cref="TextClassification.Skiped"/>,
    /// alles Übrige gilt als <see cref="TextClassification.Whitespace"/>.
    /// </summary>
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