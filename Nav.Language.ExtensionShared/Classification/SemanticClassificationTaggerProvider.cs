#region Using Directives

using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Classification; 

[ContentType(NavLanguageContentDefinitions.ContentType)]
[Export(typeof(ITaggerProvider))]
[TagType(typeof(IClassificationTag))]
[TextViewRole(PredefinedTextViewRoles.Document)]
sealed class SemanticClassificationTaggerProvider: ITaggerProvider {

    readonly IClassificationTypeRegistryService _classificationTypeRegistryService;

    [ImportingConstructor]
    public SemanticClassificationTaggerProvider(IClassificationTypeRegistryService classificationTypeRegistryService) {
        _classificationTypeRegistryService = classificationTypeRegistryService;
    }

    public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag {

        return SemanticClassificationTagger.Create(_classificationTypeRegistryService, buffer) as ITagger<T>;

    }

}