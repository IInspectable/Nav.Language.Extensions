#region Using Directives

using System.Collections.Generic;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Outlining; 

/// <summary>
/// Teil-Tagger für Outlining, der jeden Transitionsblock (<see cref="TransitionDefinitionBlockSyntax"/>)
/// zu einer aufklappbaren Region macht (eingeklappte Darstellung „Transitions"). Aufgerufen vom
/// <see cref="OutliningTagger"/>.
/// </summary>
class TransitionDefinitionBlockOutlineTagger {

    /// <summary>
    /// Liefert je mehrzeiligem Transitionsblock eine Region. Einzeilige Blöcke werden übersprungen.
    /// </summary>
    /// <param name="syntaxTreeAndSnapshot">Syntaxbaum samt zugehörigem <see cref="ITextSnapshot"/>.</param>
    /// <param name="tagCreator">Fabrik für die <see cref="IOutliningRegionTag"/>-Instanzen.</param>
    public static IEnumerable<ITagSpan<IOutliningRegionTag>> GetTags(SyntaxTreeAndSnapshot syntaxTreeAndSnapshot, IOutliningRegionTagCreator tagCreator) {

        var transitionBlocks = syntaxTreeAndSnapshot.SyntaxTree.Root.DescendantNodes<TransitionDefinitionBlockSyntax>();

        foreach (var transitionBlock in transitionBlocks) {
            var extent = transitionBlock.Extent;
                
            if (extent.IsEmptyOrMissing) {
                continue;
            }

            var startLine = syntaxTreeAndSnapshot.Snapshot.GetLineNumberFromPosition(extent.Start);
            var endLine   = syntaxTreeAndSnapshot.Snapshot.GetLineNumberFromPosition(extent.End);
            if (startLine == endLine) {
                continue;
            }

            var collapsedForm = "Transitions";
            var rgnSpan       = new SnapshotSpan(new SnapshotPoint(syntaxTreeAndSnapshot.Snapshot, extent.Start), extent.Length);
            var hintSpan      = new SnapshotSpan(new SnapshotPoint(syntaxTreeAndSnapshot.Snapshot, extent.Start), extent.Length);
            var rgnTag        = tagCreator.CreateTag(collapsedForm, hintSpan);

            yield return new TagSpan<IOutliningRegionTag>(rgnSpan, rgnTag);
        }
    }

}