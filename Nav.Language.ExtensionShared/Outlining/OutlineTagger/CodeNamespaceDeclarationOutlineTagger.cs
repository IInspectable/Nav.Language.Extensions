#region Using Directives

using System.Linq;
using System.Collections.Generic;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Outlining; 

class CodeNamespaceDeclarationOutlineTagger {

    public static IEnumerable<ITagSpan<IOutliningRegionTag>> GetTags(SyntaxTreeAndSnapshot syntaxTreeAndSnapshot, IOutliningRegionTagCreator tagCreator) {
            
        var nsDecl = syntaxTreeAndSnapshot.SyntaxTree.Root.DescendantNodes<CodeNamespaceDeclarationSyntax>().FirstOrDefault();
        if (nsDecl == null) {
            yield break;
        }

        var keywordToken = nsDecl.NamespaceprefixKeyword;
        if (keywordToken.IsMissing) {
            yield break;
        }

        var start  = keywordToken.End                      + 1;
        int length = syntaxTreeAndSnapshot.Snapshot.Length - start; // Bis zum Ende der Datei

        if (length <= 0) {
            yield break;
        }

        var span = new SnapshotSpan(new SnapshotPoint(syntaxTreeAndSnapshot.Snapshot, start), length);
        var tag  = tagCreator.CreateTag("...", span);

        yield return new TagSpan<IOutliningRegionTag>(span, tag);
    }
}