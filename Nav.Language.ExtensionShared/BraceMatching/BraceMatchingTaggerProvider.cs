#region Using Directives

using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;

using Microsoft.VisualStudio.Utilities;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.BraceMatching; 

/// <summary>
/// MEF-Provider (View-Tagger), der pro <see cref="ITextView"/> einen <see cref="BraceMatchingTagger"/>
/// für die Klammer-Hervorhebung bereitstellt. Liefert nur für die oberste Puffer-Ebene der View einen
/// Tagger (kein Highlighting in projizierten/eingebetteten Puffern).
/// </summary>
[Export(typeof (IViewTaggerProvider))]
[ContentType(NavLanguageContentDefinitions.ContentType)]
[TagType(typeof (TextMarkerTag))]
class BraceMatchingTaggerProvider : IViewTaggerProvider {

    /// <summary>
    /// Erzeugt den Klammer-Hervorhebungs-Tagger, sofern <paramref name="buffer"/> der oberste Puffer der
    /// <paramref name="textView"/> ist; andernfalls <c>null</c>.
    /// </summary>
    public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag {

        if (textView == null) {
            return null;
        }

        // Provide highlighting only on the top-level buffer
        if (textView.TextBuffer != buffer) {
            return null;
        }

        return new BraceMatchingTagger(textView, buffer) as ITagger<T>;
    }
}