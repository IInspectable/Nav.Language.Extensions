#nullable enable

#region Using Directives

using System.Linq;
using System.Collections.Immutable;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language; 

public static class SyntaxFacts {

    // Keywords. Die kanonischen Literale der Nav-Sprache, fest hinterlegt.
    public static readonly string TaskKeyword            = "task";
    public static readonly string TaskrefKeyword         = "taskref";
    public static readonly string InitKeyword            = "init";
    public static readonly string InitKeywordAlt         = InitKeyword.ToPascalcase();
    public static readonly string EndKeyword             = "end";
    public static readonly string ChoiceKeyword          = "choice";
    public static readonly string DialogKeyword          = "dialog";
    public static readonly string ViewKeyword            = "view";
    public static readonly string ExitKeyword            = "exit";
    public static readonly string OnKeyword              = "on";
    public static readonly string IfKeyword              = "if";
    public static readonly string ElseKeyword            = "else";
    public static readonly string SpontaneousKeyword     = "spontaneous";
    public static readonly string SpontKeyword           = "spont";
    public static readonly string DoKeyword              = "do";
    public static readonly string ResultKeyword          = "result";
    public static readonly string ParamsKeyword          = "params";
    public static readonly string BaseKeyword            = "base";
    public static readonly string NamespaceprefixKeyword = "namespaceprefix";
    public static readonly string UsingKeyword           = "using";
    public static readonly string CodeKeyword            = "code";
    public static readonly string GeneratetoKeyword      = "generateto";
    public static readonly string NotimplementedKeyword  = "notimplemented";
    public static readonly string AbstractmethodKeyword  = "abstractmethod";
    public static readonly string DonotinjectKeyword     = "donotinject";
    public static readonly string GoToEdgeKeyword        = "-->";
    public static readonly string NonModalEdgeKeyword    = "==>";
    public static readonly string ModalEdgeKeyword       = "o->";
    public static readonly string ModalEdgeKeywordAlt    = "*->";

    // Direktiven-Schlüsselwörter (nur im Präprozessor-Modus hinter `#` gültig). Einzige Autorität für die
    // Literale — der Lexer (PreprocessorKeywords) und die Completion beziehen sie von hier.
    public static readonly string VersionDirectiveKeyword = "version";
    public static readonly string PragmaDirectiveKeyword  = "pragma";

    // Das Direktiven-Einleitungszeichen (Präprozessor, `#…`). Einzige Autorität — der Lexer und die
    // Completion (Trigger-Char) beziehen es von hier.
    public static readonly char Hash = '#';

    public static readonly ImmutableHashSet<string> NavKeywords = new[] {
        TaskKeyword,
        TaskrefKeyword,
        InitKeyword,
        InitKeywordAlt,
        EndKeyword,
        ChoiceKeyword,
        DialogKeyword,
        ViewKeyword,
        ExitKeyword,
        OnKeyword,
        IfKeyword,
        ElseKeyword,
        SpontaneousKeyword,
        SpontKeyword,
        DoKeyword,
        GoToEdgeKeyword,
        NonModalEdgeKeyword,
        ModalEdgeKeyword,
        ModalEdgeKeywordAlt
    }.ToImmutableHashSet();

    public static bool IsNavKeyword(string value) {
        return NavKeywords.Contains(value);
    }

    public static readonly ImmutableHashSet<string> CodeKeywords = new[] {
        ResultKeyword,
        ParamsKeyword,
        BaseKeyword,
        NamespaceprefixKeyword,
        UsingKeyword,
        CodeKeyword,
        GeneratetoKeyword,
        NotimplementedKeyword,
        AbstractmethodKeyword,
        DonotinjectKeyword

    }.ToImmutableHashSet();

    public static bool IsCodeKeyword(string value) {
        return CodeKeywords.Contains(value);
    }

    public static readonly ImmutableHashSet<string> Keywords = NavKeywords.Concat(CodeKeywords).ToImmutableHashSet();

    public static bool IsKeyword(string value) {
        return Keywords.Contains(value);
    }

    public static readonly ImmutableHashSet<string> HiddenKeywords = new[] {
        SpontaneousKeyword,
        SpontKeyword,
        NotimplementedKeyword,
        ModalEdgeKeywordAlt,
        NonModalEdgeKeyword

    }.ToImmutableHashSet();

    public static bool IsHiddenKeyword(string value) {
        return HiddenKeywords.Contains(value);
    }

    public static readonly ImmutableHashSet<string> EdgeKeywords = new[] {
        GoToEdgeKeyword,
        NonModalEdgeKeyword,
        ModalEdgeKeyword,
        ModalEdgeKeywordAlt

    }.ToImmutableHashSet();

    public static bool IsEdgeKeyword(string value) {
        return EdgeKeywords.Contains(value);
    }

    /// <summary>
    /// Ob der Token-Typ ein Edge-Keyword ist — die Token-Typ-Sicht auf <see cref="IsEdgeKeyword(string)"/>
    /// (beide Schreibweisen der modalen Kante lexen zum selben <see cref="SyntaxTokenType.ModalEdgeKeyword"/>).
    /// </summary>
    public static bool IsEdgeKeyword(SyntaxTokenType type) {
        return type is SyntaxTokenType.GoToEdgeKeyword
                    or SyntaxTokenType.ModalEdgeKeyword
                    or SyntaxTokenType.NonModalEdgeKeyword;
    }

    // Die Zeichen, aus denen sich Edge-Keywords zusammensetzen (`-`, `>`, `o`, `*`). Einzige Autorität für
    // den Rückwärtslauf, der den Ersetzungsbereich einer angefangenen Edge bestimmt (Completion).
    public static readonly ImmutableHashSet<char> EdgeCharacters = EdgeKeywords.SelectMany(k => k).ToImmutableHashSet();

    public static bool IsEdgeCharacter(char c) {
        return EdgeCharacters.Contains(c);
    }

    // Punctuation. Wie die Keywords: die Zeichen entsprechen 1:1 den ursprünglichen Grammatik-Literalen.
    public static readonly char OpenBrace    = '{';
    public static readonly char CloseBrace   = '}';
    public static readonly char OpenParen    = '(';
    public static readonly char CloseParen   = ')';
    public static readonly char OpenBracket  = '[';
    public static readonly char CloseBracket = ']';
    public static readonly char LessThan     = '<';
    public static readonly char GreaterThan  = '>';
    public static readonly char Semicolon    = ';';
    public static readonly char Comma        = ',';
    public static readonly char Colon        = ':';
    public static readonly char Questionmark = '?';

    public static readonly ImmutableHashSet<char> Punctuations = new[] {
        OpenBrace,
        CloseBrace,
        OpenParen,
        CloseParen,
        OpenBracket,
        CloseBracket,
        LessThan,
        GreaterThan,
        Semicolon,
        Comma,
        Colon,
        Questionmark
    }.ToImmutableHashSet();

    public static bool IsPunctuation(string? value) {

        if (value?.Length != 1) {
            return false;
        }

        return Punctuations.Contains(value[0]);
    }

    public static bool IsPunctuation(char value) {
        return Punctuations.Contains(value);
    }

    /// <summary>
    /// Der kanonische Text eines Token-Typs mit festem Literal — gespeist aus den Punctuation-Konstanten
    /// (die einzige Autorität für die Zeichen bleibt). Für Typen ohne festen Text (Identifier, Literale,
    /// Keywords — letztere teils mit Schreibvarianten) <c>null</c>.
    /// </summary>
    public static string? GetText(SyntaxTokenType type) {
        switch (type) {
            case SyntaxTokenType.OpenBrace:    return OpenBrace.ToString();
            case SyntaxTokenType.CloseBrace:   return CloseBrace.ToString();
            case SyntaxTokenType.OpenParen:    return OpenParen.ToString();
            case SyntaxTokenType.CloseParen:   return CloseParen.ToString();
            case SyntaxTokenType.OpenBracket:  return OpenBracket.ToString();
            case SyntaxTokenType.CloseBracket: return CloseBracket.ToString();
            case SyntaxTokenType.LessThan:     return LessThan.ToString();
            case SyntaxTokenType.GreaterThan:  return GreaterThan.ToString();
            case SyntaxTokenType.Semicolon:    return Semicolon.ToString();
            case SyntaxTokenType.Comma:        return Comma.ToString();
            case SyntaxTokenType.Colon:        return Colon.ToString();
            case SyntaxTokenType.Questionmark: return Questionmark.ToString();
            default:                           return null;
        }
    }

    public static bool IsIdentifierCharacter(char c) {

        return c is >= 'a' and <= 'z' ||
               c is >= 'A' and <= 'Z' ||
               c is >= '0' and <= '9' ||
               c == 'Ä'               || c == 'Ö' || c == 'Ü' ||
               c == 'ä'               || c == 'ö' || c == 'ü' ||
               c == 'ß'               || c == '.' || c == '_';
    }

    public static bool IsValidIdentifier(string? value) {
        // Bewusst kein string.IsNullOrEmpty: die netstandard2.0-BCL trägt keine Nullable-Annotationen,
        // erst der explizite null-Vergleich lässt die Flussanalyse den Wert als nicht-null erkennen.
        if (value is null || value.Length == 0) {
            return false;
        }

        if (Keywords.Contains(value)) {
            return false;
        }

        return value.All(IsIdentifierCharacter);
    }

    // Comment strings
    public static readonly string SingleLineComment = "//";
    public static readonly string BlockCommentStart = "/*";
    public static readonly string BlockCommentEnd   = "*/";

    public static bool IsTrivia(TextClassification classification) {
        return classification == TextClassification.Comment || classification == TextClassification.Whitespace;
    }

    /// <summary>
    /// Ob der Token-Typ ein Kommentar ist (ein- oder mehrzeilig) — Teilmenge von
    /// <see cref="IsLexicalTrivia"/>.
    /// </summary>
    public static bool IsCommentTrivia(SyntaxTokenType type) {
        return type is SyntaxTokenType.SingleLineComment
                    or SyntaxTokenType.MultiLineComment;
    }

    /// <summary>
    /// Ob der Token-Typ rein <b>lexikalische</b> Trivia ist (Whitespace, Zeilenende, Kommentar) — die
    /// Autorität für diese Typmenge; <see cref="RawToken.IsTrivia"/> und die Parser-Sicht der
    /// versteckten Token leiten sich hieraus ab, statt die Menge zu duplizieren.
    /// </summary>
    public static bool IsLexicalTrivia(SyntaxTokenType type) {
        return type is SyntaxTokenType.Whitespace
                    or SyntaxTokenType.NewLine ||
               IsCommentTrivia(type);
    }

    /// <summary>
    /// Ob der Token-Typ ein Präprozessor-Token einer Direktive ist (<c>#</c> plus Rumpf und Zeilenende).
    /// Diese Token stehen nicht im flachen <see cref="SyntaxTree.Tokens"/>-Strom: der Direktiven-Vorlauf
    /// des Parsers faltet jeden Lauf zu strukturierter <see cref="SyntaxTokenType.DirectiveTrivia"/>
    /// (die Token liegen lokal am Direktiv-Knoten).
    /// </summary>
    public static bool IsPreprocessorToken(SyntaxTokenType type) {
        return type is SyntaxTokenType.HashToken
                    or SyntaxTokenType.PreprocessorKeyword
                    or SyntaxTokenType.PreprocessorText
                    or SyntaxTokenType.PreprocessorNewLine
                    or SyntaxTokenType.PreprocessorNumber
                    or SyntaxTokenType.PragmaKeyword
                    or SyntaxTokenType.VersionKeyword;
    }

    /// <summary>
    /// Ob der Token-Typ nicht-signifikante Trivia ist: lexikalische Trivia
    /// (<see cref="IsLexicalTrivia"/>) oder eines der strukturierten Trivia-Stücke
    /// (Präprozessor-Direktive, übersprungene Token) — im Unterschied zu den signifikanten, vom Parser
    /// konsumierten Token.
    /// </summary>
    public static bool IsTrivia(SyntaxTokenType type) {
        return IsLexicalTrivia(type) ||
               type is SyntaxTokenType.DirectiveTrivia
                    or SyntaxTokenType.SkippedTokensTrivia;
    }

}