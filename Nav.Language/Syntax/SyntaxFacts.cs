#region Using Directives

using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Statische Fakten der Nav-Sprache — nach dem Roslyn-Vorbild der <c>SyntaxFacts</c>: die kanonischen
/// Literale (Keywords, Edge-Operatoren, Punctuation-Zeichen, Direktiven-Schlüsselwörter) samt ihrer Mengen
/// und Zugehörigkeits-Prüfungen, die menschenlesbaren Keyword-Bedeutungen
/// (<see cref="GetKeywordDescription(string)"/>) sowie Abfragen über Token-Typen und Klassifikationen
/// (Trivia, Kommentare, Präprozessor). Einzige Autorität für die Literale — Lexer, Parser und Completion
/// beziehen sie von hier, statt sie zu duplizieren.
/// </summary>
public static class SyntaxFacts {

    // Keywords. Die kanonischen Literale der Nav-Sprache, fest hinterlegt.
    /// <summary>Das Schlüsselwort <c>task</c> — leitet eine Task-Definition bzw. einen Task-Knoten ein.</summary>
    public static readonly string TaskKeyword            = "task";
    /// <summary>Das Schlüsselwort <c>taskref</c> — leitet eine Task-Deklaration bzw. eine Include-Direktive ein.</summary>
    public static readonly string TaskrefKeyword         = "taskref";
    /// <summary>Das Schlüsselwort <c>init</c> — der Startknoten eines Tasks.</summary>
    public static readonly string InitKeyword            = "init";
    /// <summary>Die Pascal-Case-Variante <c>Init</c> — der Symbol-Name des Init-Knotens, ebenfalls in <see cref="NavKeywords"/> geführt.</summary>
    public static readonly string InitKeywordAlt         = InitKeyword.ToPascalcase();
    /// <summary>Das Schlüsselwort <c>end</c> — der Endknoten (regulärer Abschluss des Workflows).</summary>
    public static readonly string EndKeyword             = "end";
    /// <summary>Das Schlüsselwort <c>choice</c> — der Verzweigungsknoten.</summary>
    public static readonly string ChoiceKeyword          = "choice";
    /// <summary>Das Schlüsselwort <c>dialog</c> — GUI-Knoten, der einen Dialog anzeigt.</summary>
    public static readonly string DialogKeyword          = "dialog";
    /// <summary>Das Schlüsselwort <c>view</c> — GUI-Knoten, der eine View anzeigt.</summary>
    public static readonly string ViewKeyword            = "view";
    /// <summary>Das Schlüsselwort <c>exit</c> — ein benannter Ausgang eines Tasks.</summary>
    public static readonly string ExitKeyword            = "exit";
    /// <summary>Das Schlüsselwort <c>cancel</c> — der Abbrechen-Ausgang einer Transition als Kantenziel (ohne Deklaration, ab Sprachversion 2).</summary>
    public static readonly string CancelKeyword          = "cancel";
    /// <summary>Das Schlüsselwort <c>on</c> — der Trigger einer Transition.</summary>
    public static readonly string OnKeyword              = "on";
    /// <summary>Das Schlüsselwort <c>if</c> — die Bedingung (Guard) einer Transition.</summary>
    public static readonly string IfKeyword              = "if";
    /// <summary>Das Schlüsselwort <c>else</c> — der Alternativzweig zu einer <c>if</c>-Bedingung.</summary>
    public static readonly string ElseKeyword            = "else";
    /// <summary>Das Schlüsselwort <c>spontaneous</c> — spontane Transition ohne explizites Signal (verstecktes Keyword, siehe <see cref="HiddenKeywords"/>).</summary>
    public static readonly string SpontaneousKeyword     = "spontaneous";
    /// <summary>Das Schlüsselwort <c>spont</c> — Kurzform von <see cref="SpontaneousKeyword"/> (verstecktes Keyword).</summary>
    public static readonly string SpontKeyword           = "spont";
    /// <summary>Das Schlüsselwort <c>do</c> — die frei dokumentierende Handlungsanweisung einer Transition.</summary>
    public static readonly string DoKeyword              = "do";
    /// <summary>Das Code-Schlüsselwort <c>result</c> — Rückgabewert eines Tasks (<c>[result …]</c>).</summary>
    public static readonly string ResultKeyword          = "result";
    /// <summary>Das Code-Schlüsselwort <c>params</c> — Parameterliste einer Deklaration (<c>[params …]</c>).</summary>
    public static readonly string ParamsKeyword          = "params";
    /// <summary>Das Code-Schlüsselwort <c>base</c> — Basisklasse und Interfaces der generierten WFS-Klasse (<c>[base …]</c>).</summary>
    public static readonly string BaseKeyword            = "base";
    /// <summary>Das Code-Schlüsselwort <c>namespaceprefix</c> — Namespace-Präfix für den generierten Code (<c>[namespaceprefix …]</c>).</summary>
    public static readonly string NamespaceprefixKeyword = "namespaceprefix";
    /// <summary>Das Code-Schlüsselwort <c>using</c> — zusätzliche using-Direktive im generierten Code (<c>[using …]</c>).</summary>
    public static readonly string UsingKeyword           = "using";
    /// <summary>Das Code-Schlüsselwort <c>code</c> — wörtlich einzufügender Code-Schnipsel (<c>[code …]</c>).</summary>
    public static readonly string CodeKeyword            = "code";
    /// <summary>Das Code-Schlüsselwort <c>generateto</c> — Zielort für den generierten Code (<c>[generateto …]</c>).</summary>
    public static readonly string GeneratetoKeyword      = "generateto";
    /// <summary>Das Code-Schlüsselwort <c>notimplemented</c> — markiert den Member als noch nicht implementiert (verstecktes Keyword).</summary>
    public static readonly string NotimplementedKeyword  = "notimplemented";
    /// <summary>Das Code-Schlüsselwort <c>abstractmethod</c> — erzeugt eine abstrakte Methode (<c>[abstractmethod]</c>).</summary>
    public static readonly string AbstractmethodKeyword  = "abstractmethod";
    /// <summary>Das Code-Schlüsselwort <c>donotinject</c> — unterbindet die Dependency-Injection für diesen Member (<c>[donotinject]</c>).</summary>
    public static readonly string DonotinjectKeyword     = "donotinject";
    /// <summary>Der Edge-Operator <c>--&gt;</c> — ruft das Ziel auf (nicht modal).</summary>
    public static readonly string GoToEdgeKeyword        = "-->";
    /// <summary>Der Edge-Operator <c>==&gt;</c> — ruft das Ziel nicht-modal auf (verstecktes Keyword, siehe <see cref="HiddenKeywords"/>).</summary>
    public static readonly string NonModalEdgeKeyword    = "==>";
    /// <summary>Der Edge-Operator <c>o-&gt;</c> — ruft das Ziel modal auf.</summary>
    public static readonly string ModalEdgeKeyword       = "o->";

    // Continuation-Kanten (ab Sprachversion 2): `Quelle --> View o-^ Task` bzw. `--^ Task` — der GUI-Knoten
    // zeigt eine View UND setzt die Transition in einen Folge-Task fort. Eigene Kategorie, keine regulären
    // Transitions-Kanten (sie leiten keine neue Transition ein), daher bewusst nicht in NavKeywords/EdgeKeywords.
    /// <summary>Der Continuation-Edge-Operator <c>--^</c> — zeigt die GUI an und ruft unmittelbar den Folge-Task auf (nicht modal).</summary>
    public static readonly string ContinuationGoToEdgeKeyword  = "--^";
    /// <summary>Der Continuation-Edge-Operator <c>o-^</c> — zeigt die GUI an und ruft unmittelbar den Folge-Task modal auf.</summary>
    public static readonly string ContinuationModalEdgeKeyword = "o-^";

    // Direktiven-Schlüsselwörter (nur im Präprozessor-Modus hinter `#` gültig). Einzige Autorität für die
    // Literale — der Lexer (PreprocessorKeywords) und die Completion beziehen sie von hier.
    /// <summary>Das Direktiven-Schlüsselwort <c>version</c> (<c>#version &lt;N&gt;</c>) — legt die Nav-Sprachversion der Datei fest.</summary>
    public static readonly string VersionDirectiveKeyword = "version";
    /// <summary>Das Direktiven-Schlüsselwort <c>pragma</c> (<c>#pragma …</c>) — derzeit ohne bekannte Pragma-Subjekte.</summary>
    public static readonly string PragmaDirectiveKeyword  = "pragma";

    // Das Direktiven-Einleitungszeichen (Präprozessor, `#…`). Einzige Autorität — der Lexer und die
    // Completion (Trigger-Char) beziehen es von hier.
    /// <summary>Das Direktiven-Einleitungszeichen <c>#</c> (nur am Zeilenanfang eine Direktive).</summary>
    public static readonly char Hash = '#';

    /// <summary>
    /// Die Struktur-Keywords der Nav-Sprache — inklusive der Edge-Operatoren und der Pascal-Case-Variante
    /// <see cref="InitKeywordAlt"/>, ohne die Code-Schlüsselwörter (<see cref="CodeKeywords"/>).
    /// </summary>
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
        CancelKeyword,
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

    /// <summary>Ob <paramref name="value"/> ein Struktur-Keyword ist (siehe <see cref="NavKeywords"/>).</summary>
    public static bool IsNavKeyword(string value) {
        return NavKeywords.Contains(value);
    }

    /// <summary>Die Code-Schlüsselwörter der <c>[ … ]</c>-Code-Deklarationen (<c>result</c>, <c>params</c>, <c>using</c>, …).</summary>
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

    /// <summary>Ob <paramref name="value"/> ein Code-Schlüsselwort ist (siehe <see cref="CodeKeywords"/>).</summary>
    public static bool IsCodeKeyword(string value) {
        return CodeKeywords.Contains(value);
    }

    /// <summary>
    /// Alle Keywords der Nav-Sprache — die Vereinigung aus <see cref="NavKeywords"/> und
    /// <see cref="CodeKeywords"/>. Sie sind reserviert: keines ist ein gültiger Bezeichner
    /// (siehe <see cref="IsValidIdentifier"/>).
    /// </summary>
    public static readonly ImmutableHashSet<string> Keywords = NavKeywords.Concat(CodeKeywords).ToImmutableHashSet();

    /// <summary>Ob <paramref name="value"/> ein Keyword der Nav-Sprache ist (siehe <see cref="Keywords"/>).</summary>
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
        [CancelKeyword]      = "Abbruch-Ausgang einer Transition (ab Sprachversion 2): bricht die Navigation ab — sie tut nichts und bleibt ohne Re-Render auf dem aktuellen Knoten stehen. Anders als end/exit kein deklarierter Knoten, sondern nur als Kantenziel per Goto-Kante (-->) an einem Choice-Arm oder einer direkten Init-/Trigger-Kante.",
        [ChoiceKeyword]      = "Verzweigungsknoten — wählt anhand von Bedingungen (if/else) einen von mehreren Folgewegen.",
        [DialogKeyword]      = "GUI-Knoten: zeigt einen Dialog an.",
        [ViewKeyword]        = "GUI-Knoten: zeigt eine View (Ansicht) an.",
        [OnKeyword]          = "Trigger einer Transition — das Signal, das den Übergang auslöst.",
        [IfKeyword]          = "Bedingung (Guard) einer Transition — der Übergang gilt nur, wenn sie zutrifft.",
        [ElseKeyword]        = "Alternativzweig zu einer if-Bedingung.",
        [SpontaneousKeyword] = "Spontaner Übergang ohne explizites Signal.",
        [SpontKeyword]       = "Kurzform von spontaneous — spontaner Übergang ohne explizites Signal.",
        [DoKeyword]          = "Freie Handlungsanweisung zur Aktion einer Transition — rein dokumentierend, ohne Einfluss auf den generierten Code.",
        // Code-Keywords (in [ … ]-Deklarationen). `params`/`result` (und `task` weiter oben) sind host-abhängig —
        // die flachen Einträge hier sind der host-neutrale Fallback; die Bedeutung je Host liefert KeywordDescriptionsByHost.
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

    // Host-abhängige Bedeutung eines Keywords: dasselbe Literal meint je Code-Block-Host etwas anderes
    // (`[params]` am Task-Kopf = Parameter des Workflows, am Init-Knoten = dessen Parameter usw.). Diese
    // Tabelle überschreibt die flache KeywordDescriptions für die betroffenen Hosts; wo kein Eintrag steht,
    // gilt der host-neutrale Fallback aus KeywordDescriptions. Der Host selbst ist die Autorität von
    // CodeBlockFacts (dieselbe, die die Gültigkeit je Host bestimmt).
    static readonly ImmutableDictionary<(CodeBlockHost Host, string Keyword), string> KeywordDescriptionsByHost =
        new Dictionary<(CodeBlockHost, string), string> {
            // `task` ist selbst host-abhängig: der Definitionskopf definiert den Workflow (flacher Eintrag),
            // der Task-Knoten ruft innerhalb des Workflows einen anderen Task auf.
            [(CodeBlockHost.TaskNode,       TaskKeyword)]   = "Task-Knoten — ruft innerhalb des Workflows einen anderen Task (Unter-Workflow) auf.",
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
    /// delegiert für eine konkrete Kante hierher. Für die host-abhängigen Keywords
    /// (<c>task</c>, <c>params</c>, <c>result</c>) liefert diese host-neutrale Überladung den Fallback — die kontextgenaue Bedeutung
    /// geben <see cref="GetKeywordDescription(SyntaxToken)"/> bzw. <see cref="GetKeywordDescription(string, CodeBlockHost)"/>.
    /// </summary>
    public static string GetKeywordDescription(string keyword) {
        return KeywordDescriptions.TryGetValue(keyword, out var description) ? description : "";
    }

    /// <summary>
    /// Die kontextabhängige Bedeutung eines Keyword-Tokens — die Variante für die betreffende Position im
    /// Syntaxbaum (Hover/QuickInfo). Der Code-Block-Host wird aus der Ancestor-Kette des Tokens abgeleitet
    /// (<see cref="CodeBlockFacts.HostKindOf"/>); für host-abhängige Keywords (<c>task</c>, <c>params</c>,
    /// <c>result</c>) wählt er die passende Erläuterung, sonst gilt die host-neutrale <see cref="GetKeywordDescription(string)"/>.
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
    /// Die Bedeutung eines Keywords im angegebenen Code-Block-Host — die kontextgenaue Variante für die
    /// Completion, die den Host bereits kennt (<c>NavCompletionContext.Host</c>). Für host-abhängige
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

    /// <summary>
    /// Die versteckten Keywords (<c>spontaneous</c>/<c>spont</c>, <c>notimplemented</c>, <c>==&gt;</c>) —
    /// grammatisch gültig, aber in nutzerseitigen Ausgaben (Completion-Vorschläge,
    /// <c>expected …</c>-Diagnosen) unterdrückt.
    /// </summary>
    public static readonly ImmutableHashSet<string> HiddenKeywords = new[] {
        SpontaneousKeyword,
        SpontKeyword,
        NotimplementedKeyword,
        NonModalEdgeKeyword

    }.ToImmutableHashSet();

    /// <summary>Ob <paramref name="value"/> ein verstecktes Keyword ist (siehe <see cref="HiddenKeywords"/>).</summary>
    public static bool IsHiddenKeyword(string value) {
        return HiddenKeywords.Contains(value);
    }

    /// <summary>
    /// Die regulären Transitions-Kanten <c>--&gt;</c>, <c>==&gt;</c> und <c>o-&gt;</c> — ohne die
    /// Continuation-Kanten (<see cref="ContinuationEdgeKeywords"/>), die keine neue Transition einleiten.
    /// </summary>
    public static readonly ImmutableHashSet<string> EdgeKeywords = new[] {
        GoToEdgeKeyword,
        NonModalEdgeKeyword,
        ModalEdgeKeyword

    }.ToImmutableHashSet();

    /// <summary>Ob <paramref name="value"/> ein Edge-Keyword ist (siehe <see cref="EdgeKeywords"/>).</summary>
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
    /// <summary>
    /// Die Continuation-Kanten <c>--^</c> und <c>o-^</c> (ab Sprachversion 2) — eigene Menge, getrennt von
    /// den regulären <see cref="EdgeKeywords"/>: eine Continuation hängt an einem GUI-Knoten und leitet
    /// keine neue Transition ein.
    /// </summary>
    public static readonly ImmutableHashSet<string> ContinuationEdgeKeywords = new[] {
        ContinuationGoToEdgeKeyword,
        ContinuationModalEdgeKeyword

    }.ToImmutableHashSet();

    /// <summary>Ob <paramref name="value"/> eine Continuation-Kante ist (siehe <see cref="ContinuationEdgeKeywords"/>).</summary>
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
    /// <summary>
    /// Die Zeichen, aus denen sich die <see cref="EdgeKeywords"/> zusammensetzen (<c>-</c>, <c>&gt;</c>,
    /// <c>=</c>, <c>o</c>) — die Autorität für den Rückwärtslauf, der den Ersetzungsbereich einer
    /// angefangenen Edge bestimmt (Completion).
    /// </summary>
    public static readonly ImmutableHashSet<char> EdgeCharacters = EdgeKeywords.SelectMany(k => k).ToImmutableHashSet();

    /// <summary>Ob <paramref name="c"/> ein Edge-Zeichen ist (siehe <see cref="EdgeCharacters"/>).</summary>
    public static bool IsEdgeCharacter(char c) {
        return EdgeCharacters.Contains(c);
    }

    // Punctuation. Wie die Keywords: die Zeichen entsprechen 1:1 den ursprünglichen Grammatik-Literalen.
    /// <summary>Das Punctuation-Zeichen <c>{</c>.</summary>
    public static readonly char OpenBrace    = '{';
    /// <summary>Das Punctuation-Zeichen <c>}</c>.</summary>
    public static readonly char CloseBrace   = '}';
    /// <summary>Das Punctuation-Zeichen <c>(</c>.</summary>
    public static readonly char OpenParen    = '(';
    /// <summary>Das Punctuation-Zeichen <c>)</c>.</summary>
    public static readonly char CloseParen   = ')';
    /// <summary>Das Punctuation-Zeichen <c>[</c>.</summary>
    public static readonly char OpenBracket  = '[';
    /// <summary>Das Punctuation-Zeichen <c>]</c>.</summary>
    public static readonly char CloseBracket = ']';
    /// <summary>Das Punctuation-Zeichen <c>&lt;</c>.</summary>
    public static readonly char LessThan     = '<';
    /// <summary>Das Punctuation-Zeichen <c>&gt;</c>.</summary>
    public static readonly char GreaterThan  = '>';
    /// <summary>Das Punctuation-Zeichen <c>;</c>.</summary>
    public static readonly char Semicolon    = ';';
    /// <summary>Das Punctuation-Zeichen <c>,</c>.</summary>
    public static readonly char Comma        = ',';
    /// <summary>Das Punctuation-Zeichen <c>:</c>.</summary>
    public static readonly char Colon        = ':';
    /// <summary>Das Punctuation-Zeichen <c>?</c>.</summary>
    public static readonly char Questionmark = '?';

    /// <summary>Alle Punctuation-Zeichen der Nav-Sprache (siehe die <c>char</c>-Konstanten oben).</summary>
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

    /// <summary>
    /// Ob <paramref name="value"/> aus genau einem Punctuation-Zeichen besteht (siehe
    /// <see cref="Punctuations"/>); <c>false</c> auch für <c>null</c> und mehrzeichige Strings.
    /// </summary>
    public static bool IsPunctuation(string? value) {

        if (value?.Length != 1) {
            return false;
        }

        return Punctuations.Contains(value[0]);
    }

    /// <summary>Ob <paramref name="value"/> ein Punctuation-Zeichen ist (siehe <see cref="Punctuations"/>).</summary>
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

    // Reverse-Map Token-Typ → kanonisches Keyword-Literal. Gegenstück zu GetText, das nur die
    // Punctuation-Literale kennt und für Keywords null liefert; zusammen decken beide alle Token-Typen
    // mit festem Text ab. Autorität bleiben die Keyword-Konstanten oben (kein zweites Literal).
    static readonly ImmutableDictionary<SyntaxTokenType, string> KeywordTexts = new Dictionary<SyntaxTokenType, string> {
        [SyntaxTokenType.TaskKeyword]            = TaskKeyword,
        [SyntaxTokenType.TaskrefKeyword]         = TaskrefKeyword,
        [SyntaxTokenType.InitKeyword]            = InitKeyword,
        [SyntaxTokenType.EndKeyword]             = EndKeyword,
        [SyntaxTokenType.ChoiceKeyword]          = ChoiceKeyword,
        [SyntaxTokenType.DialogKeyword]          = DialogKeyword,
        [SyntaxTokenType.ViewKeyword]            = ViewKeyword,
        [SyntaxTokenType.ExitKeyword]            = ExitKeyword,
        [SyntaxTokenType.CancelKeyword]          = CancelKeyword,
        [SyntaxTokenType.OnKeyword]              = OnKeyword,
        [SyntaxTokenType.IfKeyword]              = IfKeyword,
        [SyntaxTokenType.ElseKeyword]            = ElseKeyword,
        [SyntaxTokenType.SpontaneousKeyword]     = SpontaneousKeyword,
        [SyntaxTokenType.SpontKeyword]           = SpontKeyword,
        [SyntaxTokenType.DoKeyword]              = DoKeyword,
        [SyntaxTokenType.ResultKeyword]          = ResultKeyword,
        [SyntaxTokenType.ParamsKeyword]          = ParamsKeyword,
        [SyntaxTokenType.BaseKeyword]            = BaseKeyword,
        [SyntaxTokenType.NamespaceprefixKeyword] = NamespaceprefixKeyword,
        [SyntaxTokenType.UsingKeyword]           = UsingKeyword,
        [SyntaxTokenType.CodeKeyword]            = CodeKeyword,
        [SyntaxTokenType.GeneratetoKeyword]      = GeneratetoKeyword,
        [SyntaxTokenType.NotimplementedKeyword]  = NotimplementedKeyword,
        [SyntaxTokenType.AbstractmethodKeyword]  = AbstractmethodKeyword,
        [SyntaxTokenType.DonotinjectKeyword]     = DonotinjectKeyword,
    }.ToImmutableDictionary();

    /// <summary>
    /// Das kanonische Literal eines Keyword-Token-Typs (<c>namespaceprefix</c>, <c>using</c>, …) —
    /// <c>null</c> für Typen ohne festes Keyword-Literal (Identifier, Literale, Punctuation, Edge-Operatoren).
    /// Gegenstück zu <see cref="GetText"/> (Punctuation); beide zusammen liefern den Text jedes Token-Typs
    /// mit festem Literal.
    /// </summary>
    public static string? GetKeywordText(SyntaxTokenType type) {
        return KeywordTexts.TryGetValue(type, out var text) ? text : null;
    }

    /// <summary>
    /// Ob <paramref name="c"/> in einem Nav-Bezeichner zulässig ist: ASCII-Buchstaben und -Ziffern, die
    /// deutschen Umlaute und <c>ß</c> sowie <c>.</c> und <c>_</c>.
    /// </summary>
    public static bool IsIdentifierCharacter(char c) {

        return c is >= 'a' and <= 'z' ||
               c is >= 'A' and <= 'Z' ||
               c is >= '0' and <= '9' ||
               c == 'Ä'               || c == 'Ö' || c == 'Ü' ||
               c == 'ä'               || c == 'ö' || c == 'ü' ||
               c == 'ß'               || c == '.' || c == '_';
    }

    /// <summary>
    /// Ob <paramref name="value"/> ein gültiger Nav-Bezeichner ist: nicht leer, kein reserviertes Keyword
    /// (<see cref="Keywords"/>) und ausschließlich aus zulässigen Zeichen
    /// (<see cref="IsIdentifierCharacter"/>) aufgebaut.
    /// </summary>
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
    /// <summary>Die Einleitung <c>//</c> des einzeiligen Kommentars.</summary>
    public static readonly string SingleLineComment = "//";
    /// <summary>Die Einleitung <c>/*</c> des mehrzeiligen Kommentars.</summary>
    public static readonly string BlockCommentStart = "/*";
    /// <summary>Der Abschluss <c>*/</c> des mehrzeiligen Kommentars.</summary>
    public static readonly string BlockCommentEnd   = "*/";

    /// <summary>
    /// Ob die Klassifikation Trivia auszeichnet: <see cref="TextClassification.Comment"/> oder
    /// <see cref="TextClassification.Whitespace"/>.
    /// </summary>
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
