namespace Pharmatechnik.Nav.Language.Text;

/// <summary>
/// Die Farb-/Anzeigekategorie eines Textstücks — was einem <see cref="SyntaxToken"/> (über
/// <see cref="SyntaxToken.Classification"/>) bzw. einem <see cref="ClassifiedText"/> seine
/// visuelle Bedeutung gibt (Rolle nach Roslyn-Vorbild von <c>ClassificationTypeNames</c>).
/// Die Werte werden beim Klassifizieren gesetzt — für signifikante Token vom Parser
/// (<c>NavParser</c>), für nicht-signifikante über
/// <c>SyntaxTokenFactory.TryClassifyNonSignificant</c> — und von den Hosts in konkrete Farben
/// übersetzt: die VS-Extension bildet sie in
/// <c>ClassificationTypeDefinitions.GetSyntaxTokenClassificationMap</c> auf ihre
/// <c>IClassificationType</c> ab, der LSP-Server in <c>SemanticTokensBuilder</c> auf
/// Semantic-Token-Typen.
/// </summary>
public enum TextClassification {

    /// <summary>Unbestimmte Kategorie — der Standardwert (u.a. für <see cref="SyntaxToken.Missing"/>
    /// und <see cref="SyntaxToken.Empty"/>) und der Rückfall, wenn kein anderer Fall greift.</summary>
    Unknown,
    /// <summary>Übersprungener Text aus der Fehler-Recovery (Skip-Trivia bzw. unbekannte Token) —
    /// wird wie unklassifizierter Text dargestellt, kennzeichnet aber eine Fehlerstelle.</summary>
    Skiped,
    /// <summary>Nicht-signifikanter Zwischenraum: Leerraum, Zeilenende, Dateiende.</summary>
    Whitespace,
    /// <summary>Ein Kommentar (ein- oder mehrzeilig).</summary>
    Comment,
    /// <summary>Ein reguläres Sprach-Schlüsselwort (z.B. <c>task</c>, <c>init</c>, <c>exit</c>).</summary>
    Keyword,
    /// <summary>Ein Kontroll-Schlüsselwort des Kontrollflusses (z.B. die Transitions-/GoTo-Keywords).</summary>
    ControlKeyword,
    /// <summary>Ein Satz-/Interpunktionszeichen (z.B. <c>:</c>, <c>;</c>, Klammern).</summary>
    Punctuation,
    /// <summary>Ein String-Literal.</summary>
    StringLiteral,
    /// <summary>Ein Bezeichner ohne speziellere Rolle.</summary>
    Identifier,
    /// <summary>Der Name eines Tasks.</summary>
    TaskName,
    /// <summary>Der Name eines Formulars (GUI-Form).</summary>
    FormName,
    /// <summary>Ein Typname (etwa der Klassenname eines Parameters oder eines Signals).</summary>
    TypeName,
    /// <summary>Der Name eines Choice-Knotens.</summary>
    ChoiceNode,
    /// <summary>Der Name eines GUI-Knotens (Dialog- oder View-Knoten).</summary>
    GuiNode,
    /// <summary>Der Name eines Verbindungspunkts (Init-/Exit-/End-Connection-Point).</summary>
    ConnectionPoint,
    /// <summary>Der Textinhalt einer Präprozessor-Direktive (nach dem Direktiv-Keyword).</summary>
    PreprocessorText,
    /// <summary>Toter Code — vom Analyzer als unerreichbar erkannter Quelltext.</summary>
    DeadCode,
    /// <summary>Das Schlüsselwort einer Präprozessor-Direktive (<c>#</c>, <c>pragma</c>, <c>version</c>).</summary>
    PreprocessorKeyword,
    /// <summary>Neutraler, nicht weiter klassifizierter Text (z.B. menschenlesbare Prosa in der
    /// QuickInfo); wird ohne besondere Hervorhebung dargestellt.</summary>
    Text,
    /// <summary>Der Name eines Parameters.</summary>
    ParameterName,
    /// <summary>Ein numerisches Literal (z.B. die Versionszahl einer <c>#pragma version</c>-Direktive).</summary>
    NumberLiteral,

}