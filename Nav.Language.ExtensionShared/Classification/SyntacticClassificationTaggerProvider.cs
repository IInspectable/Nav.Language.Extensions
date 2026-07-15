#region Using Directives

using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Classification;

/// <summary>
/// MEF-Provider, der pro Nav-<see cref="ITextBuffer"/> einen <see cref="SyntacticClassificationTagger"/>
/// bereitstellt (registriert auf den Nav-Inhaltstyp und den Klassifizierungs-Tag-Typ).
/// </summary>
[ContentType(NavLanguageContentDefinitions.ContentType)]
[Export(typeof(ITaggerProvider))]
[TagType(typeof(IClassificationTag))]
[TextViewRole(PredefinedTextViewRoles.Document)]
sealed class SyntacticClassificationTaggerProvider: ITaggerProvider {

    readonly IClassificationTypeRegistryService _classificationTypeRegistryService;

    /// <summary>Wird von MEF mit dem importierten Klassifizierungs-Registrierungsdienst erzeugt.</summary>
    [ImportingConstructor]
    public SyntacticClassificationTaggerProvider(IClassificationTypeRegistryService classificationTypeRegistryService) {
        _classificationTypeRegistryService = classificationTypeRegistryService;
    }

    /// <summary>Erzeugt den syntaktischen Klassifizierungs-Tagger für den angegebenen Puffer.</summary>
    public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag {
        return new SyntacticClassificationTagger(_classificationTypeRegistryService, buffer) as ITagger<T>;

    }

}