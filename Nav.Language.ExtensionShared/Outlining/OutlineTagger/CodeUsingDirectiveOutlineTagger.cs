using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

namespace Pharmatechnik.Nav.Language.Extension.Outlining; 

class CodeUsingDirectiveOutlineTagger {

    public static IEnumerable< ITagSpan<IOutliningRegionTag>> GetTags(SyntaxTreeAndSnapshot syntaxTreeAndSnapshot, IOutliningRegionTagCreator tagCreator) {

        var usingDirectives = syntaxTreeAndSnapshot.SyntaxTree.Root.DescendantNodes<CodeUsingDeclarationSyntax>().ToList();
        if (usingDirectives.Count <2) {
            yield break;
        }

        var firstUsing = usingDirectives[0];
        var lastUsing  = usingDirectives[usingDirectives.Count - 1];

        var usingKeyword = firstUsing.UsingKeyword;
        if (usingKeyword.IsMissing) {
            yield break;
        }

        var extendStart = firstUsing.Extent;
        var extendEnd   = lastUsing.Extent;

        var start  = usingKeyword.End + 1;
        int length = extendEnd.End    - start - 1; // Letzte ] noch mit anzeigen..

        if (length <= 0) {
            yield break;
        }

        var regionSpan = new SnapshotSpan(new SnapshotPoint(syntaxTreeAndSnapshot.Snapshot, start),             length);
        var hintSpan   = new SnapshotSpan(new SnapshotPoint(syntaxTreeAndSnapshot.Snapshot, extendStart.Start), extendEnd.End - extendStart.Start);
        var tag        = tagCreator.CreateTag("...", hintSpan);

        yield return new TagSpan<IOutliningRegionTag>(regionSpan, tag);
    }
}