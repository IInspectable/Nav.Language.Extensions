#region Using Directives

using System.IO;
using System.Linq;

using Microsoft.VisualStudio.Text;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Completion; 

static class TextSnaphotLineExtensions {

    public static SnapshotPoint GetStartOfIdentifier(this ITextSnapshotLine line, SnapshotPoint start) {
        while (start > line.Start && SyntaxFacts.IsIdentifierCharacter((start - 1).GetChar())) {
            start -= 1;
        }

        return start;
    }

    public static SnapshotPoint? GetPreviousNonWhitespace(this ITextSnapshotLine line, SnapshotPoint start) {

        if (start == line.Start) {
            return null;
        }

        do {
            start -= 1;
        } while (start > line.Start && char.IsWhiteSpace(start.GetChar()));

        return start;
    }

    public static SnapshotSpan? GetSpanOfPreviousIdentifier(this ITextSnapshotLine line, SnapshotPoint start) {

        var wordEnd = line.GetPreviousNonWhitespace(start);
        if (wordEnd == null) {
            return null;
        }

        var wordStart = line.GetStartOfIdentifier(wordEnd.Value);

        return new SnapshotSpan(wordStart, wordEnd.Value + 1);
    }

    public static SnapshotPoint GetStartOfFileNamePart(this ITextSnapshotLine line, SnapshotPoint start) {
        while (start > line.Start && IsFileNameChar((start - 1).GetChar())) {
            start -= 1;
        }

        return start;
    }

    static bool IsFileNameChar(this char ch) {
        return Path.GetInvalidFileNameChars().All(c => ch != c);
    }

}