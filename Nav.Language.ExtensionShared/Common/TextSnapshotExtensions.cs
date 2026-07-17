#region Using Directives

using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Text;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Common; 

/// <summary>
/// Erweiterungsmethoden über <see cref="ITextSnapshot"/>: der Voll-Span des Snapshots sowie eine
/// zeichenbasierte Vorwärts-/Rückwärtssuche mit optionaler Groß-/Kleinschreibung.
/// </summary>
static class TextSnapshotExtensions {

    /// <summary>Liefert den <see cref="SnapshotSpan"/>, der den gesamten Snapshot umfasst.</summary>
    /// <param name="snapshot">Der Snapshot.</param>
    /// <returns>Der Span von 0 bis <see cref="ITextSnapshot.Length"/>.</returns>
    public static SnapshotSpan GetFullSpan(this ITextSnapshot snapshot) {
        return new SnapshotSpan(snapshot, 0, snapshot.Length);
    }

    /// <summary>
    /// Sucht <paramref name="value"/> rückwärts ab <paramref name="startIndex"/> im Text des
    /// Snapshots.
    /// </summary>
    /// <param name="text">Der zu durchsuchende Snapshot.</param>
    /// <param name="value">Die gesuchte Zeichenkette.</param>
    /// <param name="startIndex">Die Startposition der Rückwärtssuche.</param>
    /// <param name="caseSensitive">Ob Groß-/Kleinschreibung beachtet wird.</param>
    /// <returns>Der Index des letzten Treffers, oder <c>-1</c>.</returns>
    public static int LastIndexOf(this ITextSnapshot text, string value, int startIndex, bool caseSensitive) {

        var normalized = caseSensitive ? value : CaseInsensitiveComparison.ToLower(value);
        startIndex = startIndex + normalized.Length > text.Length
            ? text.Length - normalized.Length
            : startIndex;

        for (var i = startIndex; i >= 0; i--) {
            var match = true;
            for (var j = 0; j < normalized.Length; j++) {
                // just use indexer of source text. perf of indexer depends on actual implementation of SourceText.
                // * all of our implementation at editor layer should provide either O(1) or O(logn).
                //
                // only one implementation we have that could have bad indexer perf is CompositeText with heavily modified text
                // at compiler layer but I believe that being used in find all reference will be very rare if not none.
                if (!Match(normalized[j], text[i + j], caseSensitive)) {
                    match = false;
                    break;
                }
            }

            if (match) {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Sucht <paramref name="value"/> vorwärts ab <paramref name="startIndex"/> im Text des
    /// Snapshots.
    /// </summary>
    /// <param name="text">Der zu durchsuchende Snapshot.</param>
    /// <param name="value">Die gesuchte Zeichenkette.</param>
    /// <param name="startIndex">Die Startposition der Vorwärtssuche.</param>
    /// <param name="caseSensitive">Ob Groß-/Kleinschreibung beachtet wird.</param>
    /// <returns>Der Index des ersten Treffers, oder <c>-1</c>.</returns>
    public static int IndexOf(this ITextSnapshot text, string value, int startIndex, bool caseSensitive) {
        var length     = text.Length - value.Length;
        var normalized = caseSensitive ? value : CaseInsensitiveComparison.ToLower(value);

        for (var i = startIndex; i <= length; i++) {
            var match = true;
            for (var j = 0; j < normalized.Length; j++) {
                // just use indexer of source text. perf of indexer depends on actual implementation of SourceText.
                // * all of our implementation at editor layer should provide either O(1) or O(logn).
                //
                // only one implementation we have that could have bad indexer perf is CompositeText with heavily modified text
                // at compiler layer but I believe that being used in find all reference will be very rare if not none.
                if (!Match(normalized[j], text[i + j], caseSensitive)) {
                    match = false;
                    break;
                }
            }

            if (match) {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Vergleicht zwei Zeichen; bei <paramref name="caseSensitive"/> <see langword="false"/> wird
    /// <paramref name="right"/> zuvor kleingeschrieben (<paramref name="normalizedLeft"/> wird bereits
    /// normalisiert erwartet).
    /// </summary>
    static bool Match(char normalizedLeft, char right, bool caseSensitive) {
        return caseSensitive ? normalizedLeft == right : normalizedLeft == CaseInsensitiveComparison.ToLower(right);
    }
}