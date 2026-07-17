namespace Pharmatechnik.Nav.Language;

// Die numerischen Werte sind die ursprünglich vom ANTLR-Lexer vergebenen Token-Typ-Nummern; sie sind hier
// auf feste Integer eingefroren, damit der Typ unabhängig von einer Grammatik-Codegenerierung ist. Die
// konkreten Zahlen lecken nicht in beobachtbaren Output (Golden-Dumps schreiben den Namen), sind aber 1:1
// übernommen, damit ältere persistierte/serialisierte Werte stabil bleiben.
/// <summary>
/// Der Typ eines <see cref="SyntaxToken"/> — vom Lexer (<c>NavLexer</c>) vergeben. Die kanonischen
/// Literale der Schlüsselwörter und Satzzeichen hält <see cref="SyntaxFacts"/>
/// (<see cref="SyntaxFacts.GetKeywordText"/>/<see cref="SyntaxFacts.GetText"/>).
/// </summary>
public enum SyntaxTokenType {

    /// <summary>Das Schlüsselwort <c>task</c>.</summary>
    TaskKeyword            = 1,
    /// <summary>Das Schlüsselwort <c>taskref</c>.</summary>
    TaskrefKeyword         = 2,
    /// <summary>Das Schlüsselwort <c>init</c>.</summary>
    InitKeyword            = 3,
    /// <summary>Das Schlüsselwort <c>end</c>.</summary>
    EndKeyword             = 4,
    /// <summary>Das Schlüsselwort <c>choice</c>.</summary>
    ChoiceKeyword          = 5,
    /// <summary>Das Schlüsselwort <c>dialog</c>.</summary>
    DialogKeyword          = 6,
    /// <summary>Das Schlüsselwort <c>view</c>.</summary>
    ViewKeyword            = 7,
    /// <summary>Das Schlüsselwort <c>exit</c>.</summary>
    ExitKeyword            = 8,
    /// <summary>Das Schlüsselwort <c>on</c>.</summary>
    OnKeyword              = 9,
    /// <summary>Das Schlüsselwort <c>if</c>.</summary>
    IfKeyword              = 10,
    /// <summary>Das Schlüsselwort <c>else</c>.</summary>
    ElseKeyword            = 11,
    /// <summary>Das Schlüsselwort <c>spontaneous</c>.</summary>
    SpontaneousKeyword     = 12,
    /// <summary>Das Schlüsselwort <c>spont</c> — Kurzform von <see cref="SpontaneousKeyword"/>.</summary>
    SpontKeyword           = 13,
    /// <summary>Das Schlüsselwort <c>do</c>.</summary>
    DoKeyword              = 14,
    /// <summary>Das Schlüsselwort <c>result</c>.</summary>
    ResultKeyword          = 15,
    /// <summary>Das Schlüsselwort <c>params</c>.</summary>
    ParamsKeyword          = 16,
    /// <summary>Das Schlüsselwort <c>base</c>.</summary>
    BaseKeyword            = 17,
    /// <summary>Das Schlüsselwort <c>namespaceprefix</c>.</summary>
    NamespaceprefixKeyword = 18,
    /// <summary>Das Schlüsselwort <c>using</c>.</summary>
    UsingKeyword           = 19,
    /// <summary>Das Schlüsselwort <c>code</c>.</summary>
    CodeKeyword            = 20,
    /// <summary>Das Schlüsselwort <c>generateto</c>.</summary>
    GeneratetoKeyword      = 21,
    /// <summary>Das Schlüsselwort <c>notimplemented</c>.</summary>
    NotimplementedKeyword  = 22,
    /// <summary>Das Schlüsselwort <c>abstractmethod</c>.</summary>
    AbstractmethodKeyword  = 23,
    /// <summary>Das Schlüsselwort <c>donotinject</c>.</summary>
    DonotinjectKeyword     = 24,
    /// <summary>Der Kanten-Operator <c>--&gt;</c> (Goto-Kante).</summary>
    GoToEdgeKeyword        = 25,
    /// <summary>Der Kanten-Operator <c>o-&gt;</c> (modale Kante).</summary>
    ModalEdgeKeyword       = 26,
    /// <summary>Der Kanten-Operator <c>==&gt;</c> (nicht-modale Kante).</summary>
    NonModalEdgeKeyword    = 27,
    /// <summary>Leerraum innerhalb einer Zeile (Trivia) — Zeilenumbrüche sind <see cref="NewLine"/>.</summary>
    Whitespace             = 29,
    /// <summary>Ein Zeilenkommentar <c>// …</c> (Trivia) — endet vor dem Zeilenende.</summary>
    SingleLineComment      = 30,
    /// <summary>Ein Blockkommentar <c>/* … */</c> (Trivia).</summary>
    MultiLineComment       = 31,
    /// <summary>Ein Zeilenumbruch (Trivia).</summary>
    NewLine                = 32,
    /// <summary>Ein Bezeichner (Name).</summary>
    Identifier             = 33,
    /// <summary>Das Satzzeichen <c>?</c>.</summary>
    Questionmark           = 42,
    /// <summary>Das Satzzeichen <c>{</c>.</summary>
    OpenBrace              = 34,
    /// <summary>Das Satzzeichen <c>}</c>.</summary>
    CloseBrace             = 35,
    /// <summary>Das Satzzeichen <c>(</c>.</summary>
    OpenParen              = 36,
    /// <summary>Das Satzzeichen <c>)</c>.</summary>
    CloseParen             = 37,
    /// <summary>Das Satzzeichen <c>[</c>.</summary>
    OpenBracket            = 38,
    /// <summary>Das Satzzeichen <c>]</c>.</summary>
    CloseBracket           = 39,
    /// <summary>Das Satzzeichen <c>&lt;</c>.</summary>
    LessThan               = 40,
    /// <summary>Das Satzzeichen <c>&gt;</c>.</summary>
    GreaterThan            = 41,
    /// <summary>Das Satzzeichen <c>;</c>.</summary>
    Semicolon              = 43,
    /// <summary>Das Satzzeichen <c>,</c>.</summary>
    Comma                  = 44,
    /// <summary>Das Satzzeichen <c>:</c>.</summary>
    Colon                  = 45,
    /// <summary>Ein String-Literal in doppelten Anführungszeichen.</summary>
    StringLiteral          = 46,
    /// <summary>
    /// Ein vom Lexer nicht erkanntes Einzelzeichen — auch das öffnende Zeichen eines unterminierten
    /// String-Literals bzw. Blockkommentars zerfällt hierzu.
    /// </summary>
    Unknown                = 47,
    /// <summary>Das Direktiven-Einleitungszeichen <c>#</c> (Präprozessor).</summary>
    HashToken              = 28,
    /// <summary>
    /// Ein Wort-Lauf innerhalb einer <c>#</c>-Direktive — Direktiv-Schlüsselwörter mit eigenem Token-Typ
    /// (<see cref="PragmaKeyword"/>, <see cref="VersionKeyword"/>) ausgenommen.
    /// </summary>
    PreprocessorKeyword    = 48,
    /// <summary>Sonstiger Text (Zwischenraum, Satzzeichen) innerhalb einer <c>#</c>-Direktive.</summary>
    PreprocessorText       = 50,
    /// <summary>Das Zeilenende, das eine <c>#</c>-Direktive abschließt.</summary>
    PreprocessorNewLine    = 49,
    /// <summary>Ein Ziffern-Lauf innerhalb einer <c>#</c>-Direktive (z.B. die Versionsnummer).</summary>
    PreprocessorNumber     = 51,
    /// <summary>
    /// Strukturierte Trivia einer kompletten <c>#</c>-Direktive (<see cref="DirectiveTriviaSyntax"/>) —
    /// die Präprozessor-Token liegen lokal am Direktiv-Knoten, siehe <see cref="SyntaxFacts.IsPreprocessorToken"/>.
    /// </summary>
    DirectiveTrivia        = 52,
    /// <summary>Das Direktiv-Schlüsselwort <c>pragma</c> (in <c>#pragma …</c>).</summary>
    PragmaKeyword          = 53,
    /// <summary>Das Direktiv-Schlüsselwort <c>version</c> (in <c>#pragma version …</c>).</summary>
    VersionKeyword         = 54,
    /// <summary>
    /// Strukturierte Trivia übersprungener Token (<see cref="SkippedTokensTriviaSyntax"/>) — vom Parser
    /// bei der Fehler-Recovery bzw. für <see cref="Unknown"/>-Token erzeugt.
    /// </summary>
    SkippedTokensTrivia    = 55,
    // Continuation-Kanten (`--^`/`o-^`, ab Sprachversion 2): der GUI-Knoten setzt die Transition in einen
    // Folge-Task fort (§Continuation des V2-Codegen-Designs). Getrennt von den regulären Transitions-Kanten,
    // weil sie keine neue Transition einleiten.
    /// <summary>Der Continuation-Kanten-Operator <c>--^</c> (Goto, ab Sprachversion 2).</summary>
    ContinuationGoToEdgeKeyword  = 56, // --^
    /// <summary>Der Continuation-Kanten-Operator <c>o-^</c> (modal, ab Sprachversion 2).</summary>
    ContinuationModalEdgeKeyword = 57, // o-^
    /// <summary>
    /// Das Schlüsselwort <c>cancel</c> — der Abbrechen-Ausgang einer Transition als Kantenziel (ohne
    /// Deklaration, ab Sprachversion 2).
    /// </summary>
    CancelKeyword          = 58,
    /// <summary>Das abschließende Datei-Ende-Token.</summary>
    EndOfFile              = 255

}