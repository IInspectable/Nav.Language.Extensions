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

    public SyntaxTrivia(SyntaxTokenType type, TextExtent extent) {
        Type   = type;
        Extent = extent;
    }

    public SyntaxTokenType Type   { get; }
    public TextExtent      Extent { get; }

    public int Start  => Extent.Start;
    public int Length => Extent.Length;
    public int End    => Extent.End;

    /// <summary>Der Quelltext dieser Trivia — für Tests und Debug-Ausgaben.</summary>
    public string ToString(SourceText sourceText) {
        return sourceText?.Substring(Extent) ?? String.Empty;
    }

    public override string ToString() {
        return $"{Extent} {Type}";
    }

}