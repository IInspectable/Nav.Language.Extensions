#region Using Directives

using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using Pharmatechnik.Nav.Language.Extension.GoToLocation;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.GoTo; 

/// <summary>
/// Der MEF-Provider, der je Nav-Textpuffer den <see cref="GoToTagger"/> erzeugt (Inhaltstyp
/// <see cref="NavLanguageContentDefinitions.ContentType"/>, Tag-Typ <see cref="GoToTag"/>). Die so
/// erzeugten <see cref="GoToTag"/>-Tags markieren die navigierbaren Symbole; der
/// <see cref="GoToMouseProcessor"/> fragt sie beim Ctrl-Klick über einen Tag-Aggregator ab.
/// </summary>
[Export(typeof(ITaggerProvider))]
[ContentType(NavLanguageContentDefinitions.ContentType)]
[Name("Nav/" + nameof(GoToTaggerProvider))]
[TagType(typeof(GoToTag))]
sealed class GoToTaggerProvider : ITaggerProvider {
    /// <summary>Erzeugt den <see cref="GoToTagger"/> für <paramref name="buffer"/>.</summary>
    public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag {
        return GoToTagger.Create<T>(buffer);
    }
}
