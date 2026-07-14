namespace Pharmatechnik.Nav.Language.Text;

/// <summary>
/// Fabrik für <see cref="ClassifiedText"/>-Stücke: je Methode ein Text mit der passenden
/// <see cref="TextClassification"/>. Zentrale Anlaufstelle, über die
/// <see cref="DisplayPartsBuilder"/> die Anzeige eines Symbols aus einzelnen klassifizierten
/// Teilen zusammensetzt, statt die Kategorie an jeder Stelle von Hand zu wählen.
/// </summary>
public static class ClassifiedTexts {

    /// <summary>Ein einzelnes Leerzeichen als <see cref="TextClassification.Whitespace"/>-Stück —
    /// der Trenner zwischen Anzeigeteilen.</summary>
    public static readonly ClassifiedText Space = new(" ", TextClassification.Whitespace);
    /// <summary>Ein Doppelpunkt (<see cref="SyntaxFacts.Colon"/>) als
    /// <see cref="TextClassification.Punctuation"/>-Stück.</summary>
    public static readonly ClassifiedText Colon = Punctuation(SyntaxFacts.Colon.ToString());

    /// <summary>Erzeugt ein neutrales <see cref="TextClassification.Text"/>-Stück aus einem einzelnen Zeichen.</summary>
    public static ClassifiedText Text(char c) => Text(c.ToString());
    /// <summary>Erzeugt ein neutrales <see cref="TextClassification.Text"/>-Stück (ohne besondere Hervorhebung).</summary>
    public static ClassifiedText Text(string text) => new(text,                            TextClassification.Text);
    /// <summary>Erzeugt ein <see cref="TextClassification.Keyword"/>-Stück für ein Schlüsselwort.</summary>
    public static ClassifiedText Keyword(string keyword) => new(keyword,                   TextClassification.Keyword);
    /// <summary>Erzeugt ein <see cref="TextClassification.TaskName"/>-Stück für einen Task-Namen.</summary>
    public static ClassifiedText TaskName(string taskName) => new(taskName,                TextClassification.TaskName);
    /// <summary>Erzeugt ein <see cref="TextClassification.GuiNode"/>-Stück für den Namen eines GUI-Knotens.</summary>
    public static ClassifiedText GuiNode(string formName) => new(formName,                 TextClassification.GuiNode);
    /// <summary>Erzeugt ein <see cref="TextClassification.ChoiceNode"/>-Stück für den Namen eines Choice-Knotens.</summary>
    public static ClassifiedText ChoiceNode(string formName) => new(formName,              TextClassification.ChoiceNode);
    /// <summary>Erzeugt ein Stück für einen Methodennamen; klassifiziert wie ein
    /// <see cref="TextClassification.ChoiceNode"/>.</summary>
    public static ClassifiedText MethodName(string formName) => new(formName,              TextClassification.ChoiceNode);
    /// <summary>Erzeugt ein <see cref="TextClassification.Identifier"/>-Stück für einen Bezeichner.</summary>
    public static ClassifiedText Identifier(string identifier) => new(identifier,          TextClassification.Identifier);
    /// <summary>Erzeugt ein <see cref="TextClassification.ConnectionPoint"/>-Stück für den Namen eines Verbindungspunkts.</summary>
    public static ClassifiedText ConnectionPoint(string identifier) => new(identifier,     TextClassification.ConnectionPoint);
    /// <summary>Erzeugt ein <see cref="TextClassification.Whitespace"/>-Stück für beliebigen Zwischenraum.</summary>
    public static ClassifiedText Whitespace(string whitespace) => new(whitespace,          TextClassification.Whitespace);
    /// <summary>Erzeugt ein <see cref="TextClassification.Punctuation"/>-Stück für Interpunktion.</summary>
    public static ClassifiedText Punctuation(string punctuation) => new(punctuation,       TextClassification.Punctuation);
    /// <summary>Erzeugt ein <see cref="TextClassification.StringLiteral"/>-Stück für ein String-Literal.</summary>
    public static ClassifiedText StringLiteral(string stringLiteral) => new(stringLiteral, TextClassification.StringLiteral);

}