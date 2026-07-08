#region Using Directives

using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using Pharmatechnik.Nav.Language.Extension.GoToLocation;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.GoTo; 

[Export(typeof(ITaggerProvider))]
[ContentType(NavLanguageContentDefinitions.ContentType)]
[Name("Nav/" + nameof(GoToTaggerProvider))]
[TagType(typeof(GoToTag))]
sealed class GoToTaggerProvider : ITaggerProvider {
    public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag {
        return GoToTagger.Create<T>(buffer);
    }
}