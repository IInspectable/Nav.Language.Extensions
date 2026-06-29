using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Ein Stück nicht-signifikanter Quelltext (Whitespace, Zeilenende, Kommentar), das als Leading- bzw.
/// Trailing-Trivia an einem <see cref="SyntaxToken"/> hängt — das echte Roslyn-Modell. Anders als ein
/// <see cref="SyntaxToken"/> trägt eine Trivia weder eine kontextabhängige <see cref="TextClassification"/>
/// noch einen Parent; sie ist allein durch ihren lexikalischen <see cref="Type"/> und ihren
/// <see cref="Extent"/> bestimmt. Die API ist bewusst minimal gehalten und kann später um eine
/// strukturierte Sicht (z.B. <c>GetStructure()</c> für Direktiven/Doc-Kommentare) wachsen.
/// </summary>
[Serializable]
public readonly struct SyntaxTrivia: IExtent {

    /// <summary>
    /// Erzeugt eine Trivia mit ihrem lexikalischen Typ und dem von ihr abgedeckten Quelltext-Ausschnitt.
    /// </summary>
    /// <param name="type">Der lexikalische Typ der Trivia (z.B. <see cref="SyntaxTokenType.Whitespace"/>,
    /// <see cref="SyntaxTokenType.NewLine"/>, <see cref="SyntaxTokenType.SingleLineComment"/>).</param>
    /// <param name="extent">Der Quelltext-Ausschnitt, den diese Trivia abdeckt.</param>
    public SyntaxTrivia(SyntaxTokenType type, TextExtent extent) {
        Type   = type;
        Extent = extent;
    }

    /// <summary>Der lexikalische Typ dieser Trivia (Whitespace, Zeilenende oder Kommentar).</summary>
    public SyntaxTokenType Type { get; }

    /// <summary>Der Quelltext-Ausschnitt, den diese Trivia abdeckt.</summary>
    public TextExtent Extent { get; }

    /// <summary>Die Startposition dieser Trivia im Quelltext (inklusiv).</summary>
    public int Start => Extent.Start;

    /// <summary>Die Länge dieser Trivia in Zeichen.</summary>
    public int Length => Extent.Length;

    /// <summary>Die Endposition dieser Trivia im Quelltext (exklusiv).</summary>
    public int End => Extent.End;

    /// <summary>
    /// Ob diese Trivia ein Kommentar ist (ein- oder mehrzeilig) — im Unterschied zu reinem Whitespace
    /// oder einem Zeilenende. Kommentare sind die einzige semantisch tragende Trivia-Art.
    /// </summary>
    public bool IsComment => Type == SyntaxTokenType.SingleLineComment || Type == SyntaxTokenType.MultiLineComment;

    /// <summary>
    /// Liefert den Quelltext dieser Trivia aus dem übergebenen <paramref name="sourceText"/> — gedacht für
    /// Tests und Debug-Ausgaben. Da eine Trivia keinen Parent und damit keinen Bezug auf ihren
    /// <see cref="SourceText"/> hält, muss er hier explizit übergeben werden.
    /// </summary>
    /// <param name="sourceText">Der Quelltext, aus dem der Ausschnitt geschnitten wird.</param>
    /// <returns>Der Quelltext der Trivia, oder <see cref="String.Empty"/>, wenn
    /// <paramref name="sourceText"/> <c>null</c> ist.</returns>
    public string ToString(SourceText sourceText) {
        return sourceText?.Substring(Extent) ?? String.Empty;
    }

    /// <summary>
    /// Liefert eine kompakte Kurzform (Ausschnitt und Typ) — ohne den eigentlichen Quelltext, da dieser
    /// nur mit dem zugehörigen <see cref="SourceText"/> auflösbar ist (siehe <see cref="ToString(SourceText)"/>).
    /// </summary>
    public override string ToString() {
        return $"{Extent} {Type}";
    }

}