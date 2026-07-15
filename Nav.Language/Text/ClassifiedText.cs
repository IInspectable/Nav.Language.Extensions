namespace Pharmatechnik.Nav.Language.Text;

/// <summary>
/// Ein Textstück mit angehängter Farb-/Anzeigekategorie — das kleinste Baustein für
/// hervorgehobene Anzeige (QuickInfo/Hover, Completion-Beschreibungen). Entspricht Roslyns
/// <c>TaggedText</c>/<c>SymbolDisplayPart</c>: reiner Text plus eine <see cref="TextClassification"/>,
/// die der Host in eine Farbe übersetzt. Eine Folge solcher Stücke bildet die Anzeige eines
/// Symbols (siehe <see cref="SymbolExtensions.ToDisplayParts"/>); die Factory-Methoden in
/// <see cref="ClassifiedTexts"/> erzeugen die einzelnen Teile.
/// </summary>
public sealed class ClassifiedText {

    /// <summary>
    /// Erzeugt ein klassifiziertes Textstück. Ein <c>null</c>-<paramref name="text"/> wird auf
    /// <see cref="string.Empty"/> normalisiert, sodass <see cref="Text"/> nie <c>null</c> ist.
    /// </summary>
    /// <param name="text">Der Textinhalt (<c>null</c> wird zu Leerstring).</param>
    /// <param name="classification">Die Farb-/Anzeigekategorie dieses Stücks.</param>
    public ClassifiedText(string? text, TextClassification classification) {
        Text           = text ?? "";
        Classification = classification;

    }

    /// <summary>Der Textinhalt dieses Stücks (nie <c>null</c>).</summary>
    public string             Text           { get; }
    /// <summary>Die Farb-/Anzeigekategorie, mit der der Host <see cref="Text"/> darstellt.</summary>
    public TextClassification Classification { get; }

    /// <summary>Liefert den reinen <see cref="Text"/> (ohne Klassifikation).</summary>
    public override string ToString() => Text;

}