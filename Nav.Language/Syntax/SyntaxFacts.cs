#region Using Directives

using System.Linq;
using System.Collections.Generic;
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

    // Continuation-Kanten (ab Sprachversion 2): `Quelle --> View o-^ Task` bzw. `--^ Task` — der GUI-Knoten
    // zeigt eine View UND setzt den Übergang in einen Folge-Task fort. Eigene Kategorie, keine regulären
    // Transitions-Kanten (sie leiten keine neue Transition ein), daher bewusst nicht in NavKeywords/EdgeKeywords.
    public static readonly string ContinuationGoToEdgeKeyword  = "--^";
    public static readonly string ContinuationModalEdgeKeyword = "o-^";

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
        ModalEdgeKeyword
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

    // Menschenlesbare Bedeutung je Keyword — die Erläuterungszeile für Keyword-QuickInfo und
    // Completion-Tooltips, und zugleich die einzige Autorität für die Kanten-Bedeutung:
    // IEdgeModeSymbol.Description leitet ihr Literal aus EdgeMode+IsContinuation ab und delegiert hierher.
    // Jedes Edge-Literal hat eine feste Bedeutung (`--^` ist bereits „Goto+Continuation"), daher stehen die
    // Edge-Operatoren hier gleichberechtigt neben den Wort-Keywords. Schlüssel sind die kanonischen Literale
    // (aus den Keyword-Konstanten oben) — inkl. der Pascal-Case-Variante `Init`, die als Symbol-Name des
    // Init-Knotens ebenfalls in NavKeywords geführt wird.
    static readonly ImmutableDictionary<string, string> KeywordDescriptions = new Dictionary<string, string> {
        // Struktur-Keywords
        [TaskKeyword]        = "Definiert einen Workflow (Task) als eigenständige Einheit.",
        [TaskrefKeyword]     = "Bindet einen anderen Task als Unter-Workflow ein und macht dessen Ein-/Ausgänge (init/exit/end) referenzierbar.",
        [InitKeyword]        = "Startknoten eines Tasks — der Eintrittspunkt, von dem die erste Transition ausgeht.",
        [InitKeywordAlt]     = "Startknoten eines Tasks — der Eintrittspunkt, von dem die erste Transition ausgeht.",
        [EndKeyword]         = "Endknoten — regulärer Abschluss des Workflows.",
        [ExitKeyword]        = "Exit-Knoten — benannter Ausgang eines Tasks, von außen referenzierbar.",
        [ChoiceKeyword]      = "Verzweigungsknoten — wählt anhand von Bedingungen (if/else) einen von mehreren Folgewegen.",
        [DialogKeyword]      = "GUI-Knoten: zeigt einen Dialog an.",
        [ViewKeyword]        = "GUI-Knoten: zeigt eine View (Ansicht) an.",
        [OnKeyword]          = "Trigger einer Transition — das Signal, das den Übergang auslöst.",
        [IfKeyword]          = "Bedingung (Guard) einer Transition — der Übergang gilt nur, wenn sie zutrifft.",
        [ElseKeyword]        = "Alternativzweig zu einer if-Bedingung.",
        [SpontaneousKeyword] = "Spontaner Übergang ohne explizites Signal.",
        [SpontKeyword]       = "Kurzform von spontaneous — spontaner Übergang ohne explizites Signal.",
        [DoKeyword]          = "Freie Handlungsanweisung zur Aktion einer Transition — rein dokumentierend, ohne Einfluss auf den generierten Code.",
        // Code-Keywords (in [ … ]-Deklarationen). `params`/`result` sind wirt-abhängig — die flachen Einträge
        // hier sind der host-neutrale Fallback; die konkrete Bedeutung je Wirt liefert KeywordDescriptionsByHost.
        [ResultKeyword]          = "Rückgabewert eines Tasks.",
        [ParamsKeyword]          = "Parameterliste einer Deklaration (Task, Init- oder Choice-Knoten).",
        [BaseKeyword]            = "Basisklasse und Interfaces der generierten WFS-Klasse.",
        [NamespaceprefixKeyword] = "Namespace-Präfix für den generierten Code.",
        [UsingKeyword]           = "Zusätzliche using-Direktive im generierten Code.",
        [CodeKeyword]            = "Wörtlich einzufügender Code-Schnipsel.",
        [GeneratetoKeyword]      = "Zielort für den generierten Code.",
        [AbstractmethodKeyword]  = "Erzeugt eine abstrakte Methode — die Implementierung obliegt der abgeleiteten Klasse.",
        [NotimplementedKeyword]  = "Markiert den Member als noch nicht implementiert.",
        [DonotinjectKeyword]     = "Unterbindet die Dependency-Injection für diesen Member.",
        // Präprozessor-Direktiven (hinter #)
        [VersionDirectiveKeyword] = "Legt die Nav-Sprachversion der Datei fest und schaltet versionsgebundene Features frei.",
        [PragmaDirectiveKeyword]  = "Pragma-Direktive zur Feinsteuerung (z.B. Diagnosen).",
        // Kanten (Edge-Operatoren) — je Literal eine feste Bedeutung (Autorität auch für IEdgeModeSymbol).
        [GoToEdgeKeyword]              = "Ruft das Ziel auf (nicht modal).",
        [ModalEdgeKeyword]             = "Ruft das Ziel modal auf.",
        [NonModalEdgeKeyword]          = "Ruft das Ziel nicht-modal auf.",
        [ContinuationGoToEdgeKeyword]  = "Zeigt die GUI an und ruft unmittelbar den Folge-Task auf (nicht modal).",
        [ContinuationModalEdgeKeyword] = "Zeigt die GUI an und ruft unmittelbar den Folge-Task modal auf."
    }.ToImmutableDictionary();

    // Wirt-abhängige Bedeutung eines Keywords: dasselbe Literal meint je Code-Block-Wirt etwas anderes
    // (`[params]` am Task-Kopf = Parameter des Workflows, am Init-Knoten = dessen Parameter usw.). Diese
    // Tabelle überschreibt die flache KeywordDescriptions für die betroffenen Wirte; wo kein Eintrag steht,
    // gilt der host-neutrale Fallback aus KeywordDescriptions. Der Wirt selbst ist die Autorität von
    // CodeBlockFacts (dieselbe, die die Gültigkeit je Wirt bestimmt).
    static readonly ImmutableDictionary<(CodeBlockHost Host, string Keyword), string> KeywordDescriptionsByHost =
        new Dictionary<(CodeBlockHost, string), string> {
            [(CodeBlockHost.TaskDefinition, ParamsKeyword)] = "Parameterliste des Workflows (WFS).",
            [(CodeBlockHost.InitNode,       ParamsKeyword)] = "Parameterliste eines Init-Knotens.",
            [(CodeBlockHost.ChoiceNode,     ParamsKeyword)] = "Parameterliste eines Choice-Knotens.",
            [(CodeBlockHost.TaskDefinition, ResultKeyword)] = "Rückgabewert des Workflows.",
            [(CodeBlockHost.TaskRef,        ResultKeyword)] = "Rückgabewert des referenzierten Tasks (taskref)."
        }.ToImmutableDictionary();

    /// <summary>
    /// Die menschenlesbare Bedeutung eines Keywords — <see cref="System.String.Empty"/>, wenn
    /// <paramref name="keyword"/> keins ist bzw. keine hinterlegte Beschreibung hat. Umfasst auch die
    /// Edge-Operatoren (je Literal eine feste Bedeutung); <see cref="IEdgeModeSymbol.Description"/>
    /// delegiert für eine konkrete Kante hierher. Host-neutral: für die wirt-abhängigen Keywords
    /// (<c>params</c>/<c>result</c>) liefert diese Überladung den Fallback — die kontextgenaue Bedeutung
    /// geben <see cref="GetKeywordDescription(SyntaxToken)"/> bzw. <see cref="GetKeywordDescription(string, CodeBlockHost)"/>.
    /// </summary>
    public static string GetKeywordDescription(string keyword) {
        return KeywordDescriptions.TryGetValue(keyword, out var description) ? description : "";
    }

    /// <summary>
    /// Die kontextabhängige Bedeutung eines Keyword-Tokens — die Variante für die betreffende Position im
    /// Syntaxbaum (Hover/QuickInfo). Der Code-Block-Wirt wird aus der Ancestor-Kette des Tokens abgeleitet
    /// (<see cref="CodeBlockFacts.HostKindOf"/>); für wirt-abhängige Keywords (<c>params</c>/<c>result</c>)
    /// wählt er die passende Erläuterung, sonst gilt die host-neutrale <see cref="GetKeywordDescription(string)"/>.
    /// <see cref="System.String.Empty"/>, wenn das Token kein Keyword mit hinterlegter Beschreibung ist.
    /// </summary>
    public static string GetKeywordDescription(SyntaxToken token) {

        var keyword = token.ToString();

        foreach (var node in token.Parent?.AncestorsAndSelf() ?? Enumerable.Empty<SyntaxNode>()) {
            if (CodeBlockFacts.HostKindOf(node) is { } host) {
                return GetKeywordDescription(keyword, host);
            }
        }

        return GetKeywordDescription(keyword);
    }

    /// <summary>
    /// Die Bedeutung eines Keywords im angegebenen Code-Block-Wirt — die kontextgenaue Variante für die
    /// Completion, die den Wirt bereits kennt (<c>NavCompletionContext.Host</c>). Für wirt-abhängige
    /// Keywords die passende Erläuterung, sonst die host-neutrale <see cref="GetKeywordDescription(string)"/>.
    /// </summary>
    internal static string GetKeywordDescription(string keyword, CodeBlockHost host) {
        return KeywordDescriptionsByHost.TryGetValue((host, keyword), out var description)
                   ? description
                   : GetKeywordDescription(keyword);
    }

    /// <summary>
    /// Ob die Klassifikation ein Keyword-Token auszeichnet (reguläres Keyword, Kontroll-Keyword oder
    /// Präprozessor-Keyword) — die Autorität, mit der Keyword-Token von gleichnamigen Bezeichnern
    /// abgegrenzt werden (die Direktiv-Keywords <c>version</c>/<c>pragma</c> sind nicht reserviert).
    /// </summary>
    public static bool IsKeywordClassification(TextClassification classification) {
        return classification is TextClassification.Keyword
            or TextClassification.ControlKeyword
            or TextClassification.PreprocessorKeyword;
    }

    public static readonly ImmutableHashSet<string> HiddenKeywords = new[] {
        SpontaneousKeyword,
        SpontKeyword,
        NotimplementedKeyword,
        NonModalEdgeKeyword

    }.ToImmutableHashSet();

    public static bool IsHiddenKeyword(string value) {
        return HiddenKeywords.Contains(value);
    }

    public static readonly ImmutableHashSet<string> EdgeKeywords = new[] {
        GoToEdgeKeyword,
        NonModalEdgeKeyword,
        ModalEdgeKeyword

    }.ToImmutableHashSet();

    public static bool IsEdgeKeyword(string value) {
        return EdgeKeywords.Contains(value);
    }

    /// <summary>
    /// Ob der Token-Typ ein Edge-Keyword ist — die Token-Typ-Sicht auf <see cref="IsEdgeKeyword(string)"/>.
    /// Die Continuation-Kanten (<c>--^</c>/<c>o-^</c>) gehören <b>nicht</b> dazu (siehe
    /// <see cref="IsContinuationEdgeKeyword(SyntaxTokenType)"/>) — sie leiten keine neue Transition ein.
    /// </summary>
    public static bool IsEdgeKeyword(SyntaxTokenType type) {
        return type is SyntaxTokenType.GoToEdgeKeyword
            or SyntaxTokenType.ModalEdgeKeyword
            or SyntaxTokenType.NonModalEdgeKeyword;
    }

    // Die Continuation-Kanten (ab Sprachversion 2). Eigene Menge, getrennt von den regulären
    // <see cref="EdgeKeywords"/>: eine Continuation hängt an einem GUI-Knoten und leitet — anders als eine
    // Transitions-Kante — keine neue Transition ein.
    public static readonly ImmutableHashSet<string> ContinuationEdgeKeywords = new[] {
        ContinuationGoToEdgeKeyword,
        ContinuationModalEdgeKeyword

    }.ToImmutableHashSet();

    public static bool IsContinuationEdgeKeyword(string value) {
        return ContinuationEdgeKeywords.Contains(value);
    }

    /// <summary>
    /// Ob der Token-Typ eine Continuation-Kante ist (<c>--^</c>/<c>o-^</c>) — die Token-Typ-Sicht auf
    /// <see cref="IsContinuationEdgeKeyword(string)"/>.
    /// </summary>
    public static bool IsContinuationEdgeKeyword(SyntaxTokenType type) {
        return type is SyntaxTokenType.ContinuationGoToEdgeKeyword
            or SyntaxTokenType.ContinuationModalEdgeKeyword;
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
