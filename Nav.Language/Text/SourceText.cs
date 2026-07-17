#region Using Directives

using System;
using System.IO;

using Pharmatechnik.Nav.Language.Internal;

#endregion

namespace Pharmatechnik.Nav.Language.Text;

/// <summary>
/// Abstrakte Repräsentation eines Quelltexts samt Zeilenstruktur. Ein <see cref="SourceText"/>
/// bietet zeichen- und spanbasierten Zugriff auf den Inhalt sowie die Abbildung zwischen
/// Zeichen-Offsets und Zeilen-/Spalten-Positionen.
/// <para>
/// Positionen sind Zeichen-Offsets in <c>[0, Length]</c>; Extents und Bereiche verstehen ihr
/// Ende stets <em>exklusiv</em>. Die <see cref="TextLines"/> sind lückenlos, aufsteigend und
/// decken den gesamten Text ab; es gibt immer mindestens eine Zeile (auch bei leerem Text).
/// </para>
/// </summary>
public abstract class SourceText {

    /// <summary>
    /// Die zugrunde liegende Datei, oder <c>null</c>, wenn der Text nicht aus einer Datei stammt.
    /// </summary>
    public abstract FileInfo? FileInfo { get; }

    /// <summary>
    /// Der gesamte Textinhalt als Zeichenkette.
    /// </summary>
    public abstract string Text { get; }

    /// <summary>
    /// Der gesamte Textinhalt als <see cref="ReadOnlySpan{T}"/> – allokationsfreier Zugriff auf die Zeichen.
    /// </summary>
    public abstract ReadOnlySpan<char> Span { get; }

    /// <summary>
    /// Die Anzahl der Zeichen im Text.
    /// </summary>
    public abstract int Length { get; }

    /// <summary>
    /// Die Zeilen des Texts. Die Zeilen sind lückenlos, aufsteigend und decken den Bereich
    /// <c>[0, Length]</c> vollständig ab; es gibt immer mindestens eine Zeile.
    /// </summary>
    public abstract SourceTextLineList TextLines { get; }

    /// <summary>
    /// Liefert den Teilstring, den <paramref name="textExtent"/> beschreibt.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Der Extent liegt außerhalb von <c>[0, Length]</c>.</exception>
    public string Substring(TextExtent textExtent) {
        return Substring(textExtent.Start, textExtent.Length);
    }

    /// <summary>
    /// Liefert den Teilstring ab <paramref name="startIndex"/> mit der angegebenen <paramref name="length"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Der angeforderte Bereich liegt außerhalb von <c>[0, Length]</c>.
    /// </exception>
    public string Substring(int startIndex, int length) {
        return Slice(startIndex: startIndex, length: length).ToString();
    }

    /// <summary>
    /// Liefert den von <paramref name="textExtent"/> beschriebenen Ausschnitt als Span.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Der Extent liegt außerhalb von <c>[0, Length]</c>.</exception>
    public ReadOnlySpan<char> Slice(TextExtent textExtent) {
        return Slice(textExtent.Start, textExtent.Length);
    }

    /// <summary>
    /// Liefert den Ausschnitt ab <paramref name="startIndex"/> mit der angegebenen <paramref name="length"/> als Span.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Der angeforderte Bereich liegt außerhalb von <c>[0, Length]</c>.
    /// </exception>
    public ReadOnlySpan<char> Slice(int startIndex, int length) {
        return Span.Slice(start: startIndex, length: length);
    }

    /// <summary>
    /// Erzeugt einen <see cref="SourceText"/> aus der angegebenen Zeichenkette.
    /// </summary>
    /// <param name="text">Der Textinhalt.</param>
    /// <param name="filePath">Optionaler Pfad der zugehörigen Datei; <c>null</c> für Text ohne Datei.</param>
    public static SourceText From(string text, string? filePath = null) {
        return new StringSourceText(text: text, filePath: filePath);
    }

    /// <summary>
    /// Ein gemeinsam genutzter, leerer <see cref="SourceText"/> (Länge 0, eine leere Zeile).
    /// Es handelt sich um ein Singleton – jeder Zugriff liefert dieselbe Instanz.
    /// </summary>
    public static SourceText Empty { get; } = new StringSourceText(null, null);

    /// <summary>
    /// Liefert das Zeichen am angegebenen Index.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> liegt außerhalb von <c>[0, Length)</c>.</exception>
    public abstract char this[int index] { get; }

    /// <summary>
    /// Bildet den angegebenen <paramref name="extent"/> auf eine <see cref="Location"/> ab (Datei,
    /// Zeichenbereich und Zeilen-/Spalten-Bereich). Das Ende des Extents ist exklusiv; endet er exakt
    /// an einem Zeilenende, liegt die End-Position auf der Folgezeile (Character 0). Ein Extent, der
    /// genau am Dateiende beginnt und endet (<c>[Length, Length]</c>), ist zulässig.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="extent"/> ist <c>missing</c> oder sein Ende liegt hinter <see cref="Length"/>.
    /// </exception>
    public Location GetLocation(TextExtent extent) {

        if (extent.IsMissing) {
            throw new ArgumentOutOfRangeException(nameof(extent), extent, "Der Extent darf nicht 'missing' sein.");
        }

        if (extent.End > Length) {
            throw new ArgumentOutOfRangeException(nameof(extent), extent,
                                                  $"Das Ende des Extents ({extent.End}) liegt außerhalb des Texts (Länge {Length}).");
        }

        return new Location(extent, GetLineRange(extent), FileInfo?.FullName);
    }

    /// <summary>
    /// Liefert den gesamten Textinhalt (identisch zu <see cref="Text"/>).
    /// </summary>
    public override string ToString() {
        return Text;
    }

    /// <summary>
    /// Liefert die Zeile, die die angegebene Position (Zeichen-Offset, nicht Zeilennummer)
    /// enthält. Gültig ist der Bereich <c>[0, Length]</c>: <c>position == Length</c> ist
    /// bewusst erlaubt und liefert die letzte Zeile (Position am Dateiende).
    /// </summary>
    /// <param name="position">Der Zeichen-Offset im Text.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="position"/> liegt außerhalb von <c>[0, Length]</c>.
    /// </exception>
    public SourceTextLine GetTextLineAtPosition(int position) {
        if (position < 0 || position > Length) {
            throw new ArgumentOutOfRangeException(nameof(position));
        }

        return GetTextLineAtPositionCore(position);
    }

    LineRange GetLineRange(TextExtent extent) {

        var start = GetLinePositionAtPosition(extent.Start);
        var end   = GetLinePositionAtPosition(extent.End);

        return new LineRange(start, end);
    }

    LinePosition GetLinePositionAtPosition(int position) {
        var lineInformation = GetTextLineAtPositionCore(position);
        return new LinePosition(lineInformation.Line, position - lineInformation.Extent.Start);
    }

    int _lastLineNumber;

    SourceTextLine GetTextLineAtPositionCore(int position) {

        if (position == 0) {
            return TextLines[0];
        }

        if (position == Length) {
            return TextLines[TextLines.Count - 1];
        }

        // Natürlich ist der Zugriff auf _lastLineNumber nicht "Threadsafe". Das macht aber auch nichts. Wir verwenden den Wert nur als Hint,
        // da davon auszugehen ist, dass die Zugriffe auf die Zeileninformationen immer in etwa im selben Bereich stattfinden. Im worst case
        // werden ohnehin alle Zeilen durchsucht.
        //
        // Effektive Reichweite des Fensters ist hintWindow - 1 (= 3): Der Treffer fällt erst, wenn die Schleife den *Start der Folgezeile*
        // sieht (position < TextLines[i].Start → Zeile i-1). Für eine Position in Zeile lastLineNumber + (hintWindow - 1) liegt dieser
        // Folgezeilen-Start bereits außerhalb des Fensters, und die Suche fällt korrekt in die Binärsuche zurück — dasselbe Ergebnis,
        // nur langsamer.
        const int hintWindow     = 4;
        var       lastLineNumber = _lastLineNumber;

        if (position >= TextLines[lastLineNumber].Start) {
            var limit = Math.Min(TextLines.Count, lastLineNumber + hintWindow);
            for (var i = lastLineNumber; i < limit; i++) {
                if (position < TextLines[i].Start) {
                    var lineNumber = i - 1;
                    _lastLineNumber = lineNumber;
                    return TextLines[lineNumber];
                }
            }
        }

        var textLine = TextLines.FindElementAtPosition(position);
        _lastLineNumber = textLine.Line;

        return textLine;
    }

}
