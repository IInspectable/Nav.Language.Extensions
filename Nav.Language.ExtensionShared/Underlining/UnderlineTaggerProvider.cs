#region Using Directives

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Underlining; 

/// <summary>
/// MEF-Provider, der pro Nav-<see cref="ITextBuffer"/> den <see cref="UnderlineTagger"/>-Singleton als
/// <see cref="UnderlineTag"/>-Tagger bereitstellt.
/// </summary>
[Export(typeof(ITaggerProvider))]
[ContentType(NavLanguageContentDefinitions.ContentType)]
[TagType(typeof(UnderlineTag))]
sealed class UnderlineTaggerProvider : ITaggerProvider {
    /// <summary>Liefert bzw. erzeugt den an den Puffer gebundenen <see cref="UnderlineTagger"/>.</summary>
    public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag {
        return UnderlineTagger.GetOrCreateSingelton<T>(buffer);
    }
}