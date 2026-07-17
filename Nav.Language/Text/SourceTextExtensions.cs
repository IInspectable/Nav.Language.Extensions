using System;

namespace Pharmatechnik.Nav.Language.Text;

static class SourceTextExtensions {

    /// <summary>
    /// Liefert den Span vom Zeilenanfang bis zur angegebenen Position im Text.
    /// "Zeile mit Text"
    ///  ^-----^
    /// </summary>
    public static ReadOnlySpan<char> SliceFromLineStartToPosition(this SourceText sourceText, int toPosition) {
        var line = sourceText.GetTextLineAtPosition(toPosition);
        return sourceText.Slice(startIndex: line.Start, length: toPosition - line.Start);
    }

    /// <summary>
    /// Liefert den Span von der angegebenen Position im Text bis zum Zeilenende
    /// "Zeile mit Text"
    ///        ^------^
    /// </summary>
    public static ReadOnlySpan<char> SliceFromPositionToLineEnd(this SourceText sourceText, int fromPosition) {
        var line = sourceText.GetTextLineAtPosition(fromPosition);
        return sourceText.Slice(startIndex: fromPosition, length: line.End - fromPosition);
    }

    /// <summary>
    /// Liefert die Anzahl der Spalten zwischen zwei <see cref="Location"/>s (unter Berücksichtigung von
    /// Tabulatoren gemäß <paramref name="textEditorSettings"/>). Liegen die Locations in unterschiedlichen
    /// Zeilen, zählt die Startspalte der zweiten Location; in derselben Zeile der Abstand zwischen dem Ende
    /// der ersten und dem Anfang der zweiten. Das Ergebnis ist stets mindestens 1; bei fehlender Location 0.
    /// </summary>
    public static int ColumnsBetweenLocations(this SourceText sourceText, Location? location1, Location? location2, TextEditorSettings textEditorSettings) {

        if (location1 == null || location2 == null) {
            return 0;
        }

        int spaceCount;
        if (location1.EndLine != location2.StartLine) {
            // Locations in unterschiedliche Zeilen
            var column = sourceText.GetStartColumn(location2, textEditorSettings);
            spaceCount = column;
        } else {
            // Locations in selber Zeile
            var startColumn = sourceText.GetEndColumn(location1, textEditorSettings);
            var endColumn   = sourceText.GetStartColumn(location2, textEditorSettings);

            spaceCount = Math.Max(1, endColumn - startColumn);
        }

        return Math.Max(1, spaceCount);
    }

    /// <summary>
    /// Liefert die Spalte (unter Berücksichtigung von Tabulatoren) am Ende der angegebenen <paramref name="location"/>.
    /// </summary>
    public static int GetEndColumn(this SourceText sourceText, Location location, TextEditorSettings textEditorSettings) {

        var endLineExtent = sourceText.GetTextLineAtPosition(location.End);
        var length        = location.EndLinePosition.Character;
        var column        = endLineExtent.GetColumnForOffset(textEditorSettings.TabSize, length);

        return column;
    }

    /// <summary>
    /// Liefert die Spalte (unter Berücksichtigung von Tabulatoren) am Anfang der angegebenen <paramref name="location"/>.
    /// </summary>
    public static int GetStartColumn(this SourceText sourceText, Location location, TextEditorSettings textEditorSettings) {

        var startLineExtent = sourceText.GetTextLineAtPosition(location.Start);
        var length          = location.StartLinePosition.Character;
        var column          = startLineExtent.GetColumnForOffset(textEditorSettings.TabSize, length);

        return column;
    }

}
