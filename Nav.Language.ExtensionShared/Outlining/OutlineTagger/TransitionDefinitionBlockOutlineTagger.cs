#region Using Directives

using System.Collections.Generic;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Outlining; 

class TransitionDefinitionBlockOutlineTagger {

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