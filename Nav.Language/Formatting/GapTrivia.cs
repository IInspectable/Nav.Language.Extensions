namespace Pharmatechnik.Nav.Language.Formatting;

/// <summary>
/// Vorberechnete, formatierungs-invariante Fakten über die Trivia einer Lücke — die einzige Sicht, die
/// Regeln auf den Lückeninhalt bekommen (nie das aktuelle Whitespace, außer der Newline-Anzahl).
/// Der Lückeninhalt zwischen zwei aufeinanderfolgenden signifikanten Token A und B ist exakt
/// <c>A.TrailingTrivia ++ B.LeadingTrivia</c> (zusammenhängend, disjunkt).
/// </summary>
readonly struct GapTrivia {

    GapTrivia(bool hasComment, bool hasSkippedTokens, bool hasDirective, int newLineCount) {
        HasComment       = hasComment;
        HasSkippedTokens = hasSkippedTokens;
        HasDirective     = hasDirective;
        NewLineCount     = newLineCount;
    }

    /// <summary>Ob die Lücke mindestens einen Kommentar enthält (ein- oder mehrzeilig).</summary>
    public bool HasComment { get; }

    /// <summary>Ob die Lücke eine <see cref="SyntaxTokenType.SkippedTokensTrivia"/> schneidet (Recovery/Unknown).</summary>
    public bool HasSkippedTokens { get; }

    /// <summary>Ob die Lücke eine Direktive (<see cref="SyntaxTokenType.DirectiveTrivia"/>) enthält.</summary>
    public bool HasDirective { get; }

    /// <summary>
    /// Anzahl der <see cref="SyntaxTokenType.NewLine"/>-Trivia in der Lücke. Newlines <b>im Inneren</b>
    /// eines mehrzeiligen Kommentars zählen nicht — sie sind Teil des Kommentar-Texts.
    /// </summary>
    public int NewLineCount { get; }

    /// <summary>Sammelt die Fakten über den Lückeninhalt zwischen <paramref name="prev"/> und <paramref name="next"/>.</summary>
    public static GapTrivia Create(SyntaxToken prev, SyntaxToken next) {

        var hasComment       = false;
        var hasSkippedTokens = false;
        var hasDirective     = false;
        var newLineCount     = 0;

        foreach (var trivia in prev.TrailingTrivia) {
            Accumulate(trivia, ref hasComment, ref hasSkippedTokens, ref hasDirective, ref newLineCount);
        }

        foreach (var trivia in next.LeadingTrivia) {
            Accumulate(trivia, ref hasComment, ref hasSkippedTokens, ref hasDirective, ref newLineCount);
        }

        return new GapTrivia(hasComment, hasSkippedTokens, hasDirective, newLineCount);
    }

    static void Accumulate(in SyntaxTrivia trivia, ref bool hasComment, ref bool hasSkippedTokens,
                           ref bool hasDirective, ref int newLineCount) {

        switch (trivia.Type) {
            case SyntaxTokenType.SingleLineComment:
            case SyntaxTokenType.MultiLineComment:
                hasComment = true;
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
