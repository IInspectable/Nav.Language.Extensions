#region Using Directives

using System.IO;
using System.Linq;

using Microsoft.VisualStudio.Text;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Completion; 

/// <summary>
/// Erweiterungsmethoden auf <see cref="ITextSnapshotLine"/>, die aus einer Cursor-Position den passenden
/// Bezeichner- bzw. Dateinamen-Span bestimmen — Grundlage für den Ersetzungsbereich der Completion (siehe
/// <see cref="NavCompletionSource"/>). Sie stützen sich auf <see cref="SyntaxFacts.IsIdentifierCharacter"/>
/// bzw. die für Dateinamen ungültigen Zeichen.
/// </summary>
static class TextSnaphotLineExtensions {

    /// <summary>Wandert von <paramref name="start"/> nach links bis zum Beginn des Bezeichners und liefert diese Position.</summary>
    public static SnapshotPoint GetStartOfIdentifier(this ITextSnapshotLine line, SnapshotPoint start) {
        while (start > line.Start && SyntaxFacts.IsIdentifierCharacter((start - 1).GetChar())) {
            start -= 1;
        }

        return start;
    }

    /// <summary>Wandert von <paramref name="end"/> nach rechts bis zum Ende des Bezeichners und liefert diese Position.</summary>
    public static SnapshotPoint GetEndOfIdentifier(this ITextSnapshotLine line, SnapshotPoint end) {
        while (end < line.End && SyntaxFacts.IsIdentifierCharacter(end.GetChar())) {
            end += 1;
        }

        return end;
    }

    /// <summary>
    /// Liefert die letzte Nicht-Leerraum-Position vor <paramref name="start"/> innerhalb der Zeile, oder
    /// <c>null</c>, wenn <paramref name="start"/> bereits am Zeilenanfang steht.
    /// </summary>
    public static SnapshotPoint? GetPreviousNonWhitespace(this ITextSnapshotLine line, SnapshotPoint start) {

        if (start == line.Start) {
            return null;
        }

        do {
            start -= 1;
        } while (start > line.Start && char.IsWhiteSpace(start.GetChar()));

        return start;
    }

    /// <summary>
    /// Liefert den Span des Bezeichners, der <paramref name="start"/> (über etwaigen Leerraum hinweg)
    /// unmittelbar vorausgeht, oder <c>null</c>, wenn keiner existiert.
    /// </summary>
    public static SnapshotSpan? GetSpanOfPreviousIdentifier(this ITextSnapshotLine line, SnapshotPoint start) {

        var wordEnd = line.GetPreviousNonWhitespace(start);
        if (wordEnd == null) {
            return null;
        }

        var wordStart = line.GetStartOfIdentifier(wordEnd.Value);

        return new SnapshotSpan(wordStart, wordEnd.Value + 1);
    }

    /// <summary>
    /// Wandert von <paramref name="start"/> nach links über gültige Dateinamen-Zeichen hinweg bis zum
    /// letzten Pfadtrenner und liefert diese Position — den Anfang des getippten Dateinamen-Teils, gegen
    /// den die Pfad-Vervollständigung gefiltert wird.
    /// </summary>
    public static SnapshotPoint GetStartOfFileNamePart(this ITextSnapshotLine line, SnapshotPoint start) {
        while (start > line.Start && IsFileNameChar((start - 1).GetChar())) {
            start -= 1;
        }

        return start;
    }

    /// <summary>Ob <paramref name="ch"/> in einem Dateinamen zulässig ist (kein von <see cref="Path.GetInvalidFileNameChars"/> gemeldetes Zeichen).</summary>
    static bool IsFileNameChar(this char ch) {
        return Path.GetInvalidFileNameChars().All(c => ch != c);
    }

}