#region Using Directives

using System.Linq;
using System.Collections.Generic;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Outlining; 

/// <summary>
/// Teil-Tagger für Outlining, der eine aufklappbare Region über den auf die <c>namespaceprefix</c>-Angabe
/// folgenden Rest der Datei legt. Wird vom <see cref="OutliningTagger"/> aufgerufen (derzeit inaktiv
/// geschaltet).
/// </summary>
class CodeNamespaceDeclarationOutlineTagger {

    /// <summary>
    /// Liefert die Outlining-Region hinter dem <c>namespaceprefix</c>-Schlüsselwort bis zum Dateiende.
    /// Fehlt die Deklaration oder ist das Schlüsselwort nicht vorhanden, wird nichts geliefert.
    /// </summary>
    /// <param name="syntaxTreeAndSnapshot">Syntaxbaum samt zugehörigem <see cref="ITextSnapshot"/>.</param>
    /// <param name="tagCreator">Fabrik für die <see cref="IOutliningRegionTag"/>-Instanzen.</param>
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