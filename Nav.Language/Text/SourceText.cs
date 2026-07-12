#region Using Directives

using System;
using System.IO;

using Pharmatechnik.Nav.Language.Internal;

#endregion

namespace Pharmatechnik.Nav.Language.Text;

public abstract class SourceText {

    public abstract FileInfo? FileInfo { get; }

    public abstract string Text { get; }

    public abstract ReadOnlySpan<char> Span { get; }

    public abstract int Length { get; }

    public abstract SourceTextLineList TextLines { get; }

    public string Substring(TextExtent textExtent) {
        return Substring(textExtent.Start, textExtent.Length);
    }

    public string Substring(int startIndex, int length) {
        return Slice(startIndex: startIndex, length: length).ToString();
    }

    public ReadOnlySpan<char> Slice(TextExtent textExtent) {
        return Slice(textExtent.Start, textExtent.Length);
    }

    public ReadOnlySpan<char> Slice(int startIndex, int length) {
        return Span.Slice(start: startIndex, length: length);
    }

    public static SourceText From(string text, string? filePath = null) {
        return new StringSourceText(text: text, filePath: filePath);
    }

    public static SourceText Empty { get; } = new StringSourceText(null, null);

    public abstract char this[int index] { get; }

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
