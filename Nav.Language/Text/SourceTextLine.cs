using System;

namespace Pharmatechnik.Nav.Language.Text;

/// <summary>
/// Repräsentiert den Bereich (Extent) einer einzelnen Zeile innerhalb eines <see cref="Text.SourceText"/>.
/// Der Bereich schließt den Zeilenumbruch mit ein; das Ende ist exklusiv.
/// </summary>
[Serializable]
public readonly struct SourceTextLine: IExtent, IEquatable<SourceTextLine> {

    /// <summary>
    /// Erzeugt eine Zeile über dem angegebenen Bereich.
    /// </summary>
    /// <param name="sourceText">Der zugehörige Quelltext.</param>
    /// <param name="line">Die nullbasierte Zeilennummer.</param>
    /// <param name="lineStart">Der Zeichen-Offset des Zeilenanfangs.</param>
    /// <param name="lineEnd">Der Zeichen-Offset des Zeilenendes (exklusiv).</param>
    /// <exception cref="ArgumentNullException"><paramref name="sourceText"/> ist <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="line"/> ist negativ, der Bereich ist ungültig (missing) oder
    /// <paramref name="lineEnd"/> liegt hinter dem Textende.
    /// </exception>
    internal SourceTextLine(StringSourceText sourceText, int line, int lineStart, int lineEnd) {

        if (sourceText == null) {
            throw new ArgumentNullException(nameof(sourceText));
        }

        if (TextExtent.FromBounds(lineStart, lineEnd).IsMissing) {
            throw new ArgumentOutOfRangeException(nameof(lineEnd));
        }

        if (line < 0) {
            throw new ArgumentOutOfRangeException(nameof(line));
        }

        if (lineEnd > sourceText.Length) {
            throw new ArgumentOutOfRangeException(nameof(lineEnd));
        }

        SourceText = sourceText;
        Line       = line;
        Start      = lineStart;
        End        = lineEnd;
    }

    /// <summary>
    /// Der Quelltext, zu dem diese Zeile gehört.
    /// </summary>
    public SourceText SourceText { get; }

    /// <summary>
    /// Der Inhalt der Zeile (inklusive Zeilenumbruch) als Span.
    /// </summary>
    public ReadOnlySpan<char> Span => SourceText.Slice(Extent);

    /// <summary>
    /// Liefert einen Ausschnitt der Zeile ab der angegebenen Spalte (Offset ab Zeilenanfang).
    /// </summary>
    /// <param name="charPositionInLine">Der nullbasierte Offset innerhalb der Zeile.</param>
    /// <param name="length">Die Länge des Ausschnitts.</param>
    public ReadOnlySpan<char> Slice(int charPositionInLine, int length) => SourceText.Slice(Start + charPositionInLine, length);

    /// <summary>
    /// Die <see cref="Location"/> der gesamten Zeile.
    /// </summary>
    public Location Location => SourceText.GetLocation(Extent);

    /// <summary>
    /// Liefert die <see cref="Location"/> eines Bereichs innerhalb der Zeile.
    /// </summary>
    /// <param name="charPositionInLine">Der nullbasierte Offset innerhalb der Zeile.</param>
    /// <param name="length">Die Länge des Bereichs.</param>
    public Location GetLocation(int charPositionInLine, int length) {
        var start  = Extent.Start + charPositionInLine;
        var extent = new TextExtent(start: start, length: length);
        return SourceText.GetLocation(extent);
    }

    /// <summary>
    /// Die Zeilennummer. Die erste Zeile einer Datei ist Zeile 0 (nullbasierte Zeilennummerierung).
    /// </summary>
    public int Line { get; }

    /// <summary>
    /// Der Bereich (Extent) der Zeile.
    /// </summary>
    public TextExtent Extent => TextExtent.FromBounds(Start, End);

    /// <summary>
    /// Der Bereich (Extent) der Zeile ohne den abschließenden Zeilenumbruch.
    /// </summary>
    public TextExtent ExtentWithoutLineEndings => new(Extent.Start, Extent.Length - Span.GetNewLineCharCount());

    /// <summary>
    /// Der Zeichen-Offset des Zeilenanfangs.
    /// </summary>
    public int Start { get; }

    /// <summary>
    /// Der Zeichen-Offset des Zeilenendes (exklusiv).
    /// </summary>
    public int End { get; }

    /// <summary>
    /// Ermittelt, ob zwei <see cref="SourceTextLine"/> gleich sind.
    /// </summary>
    public static bool operator ==(SourceTextLine left, SourceTextLine right) {
        return left.Equals(right);
    }

    /// <summary>
    /// Ermittelt, ob zwei <see cref="SourceTextLine"/> verschieden sind.
    /// </summary>
    public static bool operator !=(SourceTextLine left, SourceTextLine right) {
        return !left.Equals(right);
    }

    /// <summary>
    /// Ermittelt, ob diese Zeile einer anderen entspricht.
    /// </summary>
    /// <param name="other">Das zu vergleichende Objekt.</param>
    public bool Equals(SourceTextLine other) {
        return other.Line == Line && other.Extent == Extent;
    }

    /// <summary>
    /// Ermittelt, ob diese Zeile dem angegebenen Objekt entspricht.
    /// </summary>
    /// <param name="obj">Das zu vergleichende Objekt.</param>
    public override bool Equals(object? obj) {
        return obj is SourceTextLine extent && Equals(extent);
    }

    /// <summary>
    /// Liefert den Inhalt der Zeile (inklusive Zeilenumbruch) als Zeichenkette.
    /// </summary>
    public override string ToString() {
        return SourceText.Substring(Extent);
    }

    /// <summary>
    /// Liefert eine Hashfunktion für <see cref="SourceTextLine"/>.
    /// </summary>
    public override int GetHashCode() {
        return Line ^ Extent.GetHashCode();
    }

}
