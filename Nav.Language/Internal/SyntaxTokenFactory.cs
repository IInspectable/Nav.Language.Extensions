#region Using Directives

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.Internal;

/// <summary>
/// Fabrik für <see cref="SyntaxToken"/>-Instanzen und die Einfärbungs-Zuordnung nicht-signifikanter Tokens.
/// Kapselt zwei Belange des Lexers/Parsers (<see cref="NavParser"/>, <see cref="NavDirectiveParser"/>):
/// das Erzeugen eines Tokens aus einem <see cref="TextExtent"/> — inklusive der Sonderbehandlung eines
/// fehlenden (<see cref="TextExtent.IsMissing"/>) Ausschnitts als <see cref="SyntaxToken.Missing"/> — sowie
/// die feste, kontextfreie <see cref="TextClassification"/> der nicht-signifikanten Token-Typen.
/// </summary>
static class SyntaxTokenFactory {

    /// <summary>
    /// Erzeugt einen <see cref="SyntaxToken"/> über dem angegebenen <paramref name="extend"/> mit dem
    /// gegebenen <paramref name="type"/>, seiner <paramref name="classification"/>, seinem
    /// <paramref name="parent"/>-Knoten sowie optionaler führender/nachlaufender Trivia. Kennzeichnet der
    /// Extent einen fehlenden Ausschnitt (<see cref="TextExtent.IsMissing"/>), wird stattdessen der
    /// gemeinsame <see cref="SyntaxToken.Missing"/> geliefert.
    /// </summary>
    public static SyntaxToken CreateToken(TextExtent extend, SyntaxTokenType type,
                                          TextClassification classification,
                                          SyntaxNode? parent,
                                          SyntaxTriviaList leadingTrivia = default,
                                          SyntaxTriviaList trailingTrivia = default) {

        if (extend.IsMissing) {
            return SyntaxToken.Missing;
        }

        var token = new SyntaxToken(parent, type, classification, extend, leadingTrivia, trailingTrivia);

        return token;
    }

    /// <summary>
    /// Ordnet einem nicht-signifikanten Token-Typ (Whitespace, Zeilenende, Kommentar, unbekanntes Zeichen,
    /// Präprozessor-Token) seine <see cref="TextClassification"/> zu. Liefert <c>false</c> für signifikante
    /// Token-Typen (die der Parser kontextabhängig selbst einfärbt).
    /// </summary>
    public static bool TryClassifyNonSignificant(SyntaxTokenType type, out TextClassification classification) {
        switch (type) {
            case SyntaxTokenType.Whitespace:
            case SyntaxTokenType.NewLine:
            case SyntaxTokenType.EndOfFile:
                classification = TextClassification.Whitespace;
                return true;
            case SyntaxTokenType.SingleLineComment:
            case SyntaxTokenType.MultiLineComment:
                classification = TextClassification.Comment;
                return true;
            case SyntaxTokenType.Unknown:
                classification = TextClassification.Skiped;
                return true;
            case SyntaxTokenType.HashToken:
            case SyntaxTokenType.PreprocessorKeyword:
            case SyntaxTokenType.PragmaKeyword:
            case SyntaxTokenType.VersionKeyword:
                classification = TextClassification.PreprocessorKeyword;
                return true;
            case SyntaxTokenType.PreprocessorText:
            case SyntaxTokenType.PreprocessorNewLine:
                classification = TextClassification.PreprocessorText;
                return true;
            case SyntaxTokenType.PreprocessorNumber:
                classification = TextClassification.NumberLiteral;
                return true;
            default:
                classification = TextClassification.Unknown;
                return false;
        }
    }

}