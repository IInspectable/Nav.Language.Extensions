using System.Collections.Generic;
using System.Linq;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

namespace Pharmatechnik.Nav.Language.Extension.Outlining;

/// <summary>
/// Teil-Tagger für Outlining, der jeden mehrzeiligen Kommentar (<c>/* … */</c>) zu einer aufklappbaren
/// Region macht. Die Kommentare werden aus der an die Token angehängten Trivia des Syntaxbaums gelesen.
/// Aufgerufen vom <see cref="OutliningTagger"/>.
/// </summary>
class MultilineCommentOutlineTagger {

    /// <summary>
    /// Liefert je mehrzeiligem Kommentar eine Region (eingeklappte Darstellung <c>/* ...</c>). Einzeilige
    /// Kommentare werden übersprungen.
    /// </summary>
    /// <param name="syntaxTreeAndSnapshot">Syntaxbaum samt zugehörigem <see cref="ITextSnapshot"/>.</param>
    /// <param name="tagCreator">Fabrik für die <see cref="IOutliningRegionTag"/>-Instanzen.</param>
    public static IEnumerable<ITagSpan<IOutliningRegionTag>> GetTags(SyntaxTreeAndSnapshot syntaxTreeAndSnapshot, IOutliningRegionTagCreator tagCreator) {

        // Mehrzeilige Kommentare aus der angehängten Trivia (Roslyn-Modell), nicht mehr aus dem flachen Strom.
        foreach(var mc in syntaxTreeAndSnapshot.SyntaxTree.DescendantTrivia().Where(t => t.Type == SyntaxTokenType.MultiLineComment)) {
            var extent = mc.Extent;

            if (extent.IsEmptyOrMissing) {
                continue;
            }

            var startLine = syntaxTreeAndSnapshot.Snapshot.GetLineNumberFromPosition(extent.Start);
            var endLine   = syntaxTreeAndSnapshot.Snapshot.GetLineNumberFromPosition(extent.End);
            if (startLine == endLine) {
                continue;
            }

            var collapsedForm = "/* ...";
            var rgnSpan       = new SnapshotSpan(new SnapshotPoint(syntaxTreeAndSnapshot.Snapshot, extent.Start), extent.Length);
            var hintSpan      = new SnapshotSpan(new SnapshotPoint(syntaxTreeAndSnapshot.Snapshot, extent.Start), extent.Length);
            var rgnTag        = tagCreator.CreateTag(collapsedForm, hintSpan);

            yield return new TagSpan<IOutliningRegionTag>(rgnSpan, rgnTag);
        }           
    }
}