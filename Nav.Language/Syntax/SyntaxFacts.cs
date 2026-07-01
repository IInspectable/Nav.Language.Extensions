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
        Colon
    }.ToImmutableHashSet();

    public static bool IsPunctuation(string value) {

        if (value?.Length != 1) {
            return false;
        }

        return Punctuations.Contains(value[0]);
    }

    public static bool IsPunctuation(char value) {
        return Punctuations.Contains(value);
    }

    public static bool IsIdentifierCharacter(char c) {

        return c is >= 'a' and <= 'z' ||
               c is >= 'A' and <= 'Z' ||
               c is >= '0' and <= '9' ||
               c == 'Ä'               || c == 'Ö' || c == 'Ü' ||
               c == 'ä'               || c == 'ö' || c == 'ü' ||
               c == 'ß'               || c == '.' || c == '_';
    }

    public static bool IsValidIdentifier(string value) {
        if (string.IsNullOrEmpty(value)) {
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
    /// Ob der Token-Typ nicht-signifikante Trivia ist (Whitespace, Zeilenende oder Kommentar) — im Unterschied
    /// zu signifikanten Token sowie zu Trennern (Präprozessor/Unknown), die zwar ebenfalls nicht geparst, aber
    /// auch nicht als Trivia angehängt werden.
    /// </summary>
    public static bool IsTrivia(SyntaxTokenType type) {
        return type is SyntaxTokenType.Whitespace
                    or SyntaxTokenType.NewLine
                    or SyntaxTokenType.SingleLineComment
                    or SyntaxTokenType.MultiLineComment
                    or SyntaxTokenType.DirectiveTrivia;
    }

}