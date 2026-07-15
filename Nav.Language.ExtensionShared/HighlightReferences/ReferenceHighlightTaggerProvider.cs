#region Using Directives

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.HighlightReferences; 

/// <summary>
/// Der MEF-Provider, der je Nav-Editor-Sicht den <see cref="ReferenceHighlightTagger"/> erzeugt.
/// Als <see cref="IViewTaggerProvider"/> (statt reinem <c>ITaggerProvider</c>) exportiert, weil das
/// Highlighting die <see cref="ITextView"/> braucht — es hängt an der Cursorposition. Greift nur für
/// den Nav-Inhaltstyp (<see cref="NavLanguageContentDefinitions.ContentType"/>) und liefert Tags vom
/// Typ <see cref="ReferenceHighlightTag"/>.
/// </summary>
[Export(typeof(IViewTaggerProvider))]
[ContentType(NavLanguageContentDefinitions.ContentType)]
[TagType(typeof(ReferenceHighlightTag))]
class ReferenceHighlightTaggerProvider : IViewTaggerProvider {

    /// <summary>
    /// Erzeugt den Tagger für <paramref name="textView"/>/<paramref name="buffer"/> — jedoch nur auf dem
    /// obersten Puffer der Sicht (<c>textView.TextBuffer == buffer</c>); für Projektions-/Unterpuffer und
    /// für eine fehlende Sicht liefert er <c>null</c>.
    /// </summary>
    public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag {

        if (textView == null) {
            return null;
        }

        // Provide highlighting only on the top-level buffer
        if (textView.TextBuffer != buffer) {
            return null;
        }

        return new ReferenceHighlightTagger(textView, buffer) as ITagger<T>;
    }
}
