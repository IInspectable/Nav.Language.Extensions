using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language.Formatting;

/// <summary>
/// Vorberechnete, formatierungs-invariante Fakten über die Trivia einer Lücke — die einzige Sicht, die
/// Regeln auf den Lückeninhalt bekommen (nie das aktuelle Whitespace, außer der Newline-Anzahl).
/// Der Lückeninhalt zwischen zwei aufeinanderfolgenden signifikanten Token A und B ist exakt
/// <c>A.TrailingTrivia ++ B.LeadingTrivia</c> (zusammenhängend, disjunkt).
/// </summary>
readonly struct GapTrivia {

    GapTrivia(bool hasComment, bool hasLineBreakingComment, bool hasSkippedTokens, bool hasDirective, int newLineCount) {
        HasComment             = hasComment;
        HasLineBreakingComment = hasLineBreakingComment;
        HasSkippedTokens       = hasSkippedTokens;
        HasDirective           = hasDirective;
        NewLineCount           = newLineCount;
    }

    /// <summary>Ob die Lücke mindestens einen Kommentar enthält (ein- oder mehrzeilig).</summary>
    public bool HasComment { get; }

    /// <summary>
    /// Ob die Lücke einen Kommentar enthält, der einen Zeilenumbruch erzwingt oder enthält: einen
    /// <c>//</c>-Kommentar (läuft bis Zeilenende — das folgende Token kann nie auf dieselbe Zeile) oder
    /// einen <b>mehrzeiligen</b> Block-Kommentar. Ein einzeiliger <c>/* */</c>-Kommentar zählt nicht —
    /// er verhält sich wie ein Inline-Token.
    /// </summary>
    public bool HasLineBreakingComment { get; }

    /// <summary>Ob die Lücke eine <see cref="SyntaxTokenType.SkippedTokensTrivia"/> schneidet (Recovery/Unknown).</summary>
    public bool HasSkippedTokens { get; }

    /// <summary>Ob die Lücke eine Direktive (<see cref="SyntaxTokenType.DirectiveTrivia"/>) enthält.</summary>
    public bool HasDirective { get; }

    /// <summary>
    /// Anzahl der <see cref="SyntaxTokenType.NewLine"/>-Trivia in der Lücke. Newlines <b>im Inneren</b>
    /// eines mehrzeiligen Kommentars zählen nicht — sie sind Teil des Kommentar-Texts.
    /// </summary>
    public int NewLineCount { get; }

    /// <summary>
    /// Sammelt die Fakten über den Lückeninhalt zwischen <paramref name="prev"/> und <paramref name="next"/>.
    /// <paramref name="sourceText"/> dient allein der Einstufung von Block-Kommentaren (mehrzeilig?).
    /// </summary>
    public static GapTrivia Create(SyntaxToken prev, SyntaxToken next, SourceText sourceText) {

        var hasComment             = false;
        var hasLineBreakingComment = false;
        var hasSkippedTokens       = false;
        var hasDirective           = false;
        var newLineCount           = 0;

        foreach (var trivia in prev.TrailingTrivia) {
            Accumulate(trivia, sourceText, ref hasComment, ref hasLineBreakingComment, ref hasSkippedTokens, ref hasDirective, ref newLineCount);
        }

        foreach (var trivia in next.LeadingTrivia) {
            Accumulate(trivia, sourceText, ref hasComment, ref hasLineBreakingComment, ref hasSkippedTokens, ref hasDirective, ref newLineCount);
        }

        return new GapTrivia(hasComment, hasLineBreakingComment, hasSkippedTokens, hasDirective, newLineCount);
    }

    /// <summary>
    /// Verrechnet eine einzelne Trivia in die laufenden Befunde: Kommentare setzen <paramref name="hasComment"/>
    /// (und, bei <c>//</c> bzw. mehrzeiligem Block-Kommentar, <paramref name="hasLineBreakingComment"/>),
    /// Skiped/Direktive/Newline die jeweiligen Flags bzw. den Zähler.
    /// </summary>
    static void Accumulate(in SyntaxTrivia trivia, SourceText sourceText, ref bool hasComment, ref bool hasLineBreakingComment,
                           ref bool hasSkippedTokens, ref bool hasDirective, ref int newLineCount) {

        switch (trivia.Type) {
            case SyntaxTokenType.SingleLineComment:
                hasComment             = true;
                hasLineBreakingComment = true;
                break;
            case SyntaxTokenType.MultiLineComment:
                hasComment = true;
                if (sourceText.Substring(trivia.Extent).IndexOf('\n') >= 0) {
                    hasLineBreakingComment = true;
                }

                break;
            case SyntaxTokenType.SkippedTokensTrivia:
                hasSkippedTokens = true;
                break;
            case SyntaxTokenType.DirectiveTrivia:
                hasDirective = true;
                break;
            case SyntaxTokenType.NewLine:
                newLineCount++;
                break;
        }
    }

}
