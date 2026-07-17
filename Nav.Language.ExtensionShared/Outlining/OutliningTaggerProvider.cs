#region Using Directives

using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using Pharmatechnik.Nav.Language.Extension.Common;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Outlining; 

/// <summary>
/// MEF-Provider, der für <c>.nav</c>-TextBuffer einen <see cref="OutliningTagger"/> bereitstellt und ihn
/// so in die Outlining-Infrastruktur von Visual Studio einklinkt (Tag-Typ <see cref="IOutliningRegionTag"/>,
/// Inhaltstyp <see cref="NavLanguageContentDefinitions.ContentType"/>).
/// </summary>
[Export(typeof(ITaggerProvider))]
[TagType(typeof(IOutliningRegionTag))]
[ContentType(NavLanguageContentDefinitions.ContentType)]
sealed class OutliningTaggerProvider: ITaggerProvider {

    readonly CodeContentControlProvider _codeContentControlProvider;

    /// <summary>
    /// Initialisiert den Provider mit dem per MEF importierten <paramref name="codeContentControlProvider"/>,
    /// den der erzeugte Tagger für die Hover-Vorschau der eingeklappten Regionen nutzt.
    /// </summary>
    [ImportingConstructor]
    public OutliningTaggerProvider(CodeContentControlProvider codeContentControlProvider) {
        _codeContentControlProvider = codeContentControlProvider;
    }
       
    /// <summary>Erzeugt einen <see cref="OutliningTagger"/> für den angegebenen <paramref name="buffer"/>.</summary>
    public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag {
        return new OutliningTagger(buffer, _codeContentControlProvider) as ITagger<T>;
    }
}