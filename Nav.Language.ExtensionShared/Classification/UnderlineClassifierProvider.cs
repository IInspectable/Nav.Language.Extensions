#region Using Directives

using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

using Pharmatechnik.Nav.Language.Extension.Underlining;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Classification; 

//[Export(typeof(IClassifierProvider))]
//[ContentType(NavLanguageContentDefinitions.ContentType)]
//sealed class UnderlineClassifierProvider : IClassifierProvider {

//    readonly IClassificationTypeRegistryService _classificationTypeRegistryService;
//    readonly IBufferTagAggregatorFactoryService _aggregatorFactory;

//    [ImportingConstructor]
//    public UnderlineClassifierProvider(IClassificationTypeRegistryService classificationTypeRegistryService,
//            IBufferTagAggregatorFactoryService aggregatorFactory) {
//        _classificationTypeRegistryService = classificationTypeRegistryService;
//        _aggregatorFactory = aggregatorFactory;
//    }

//    public IClassifier GetClassifier(ITextBuffer buffer) {
//        var underlineTagAggregator = _aggregatorFactory.CreateTagAggregator<UnderlineTag>(buffer);
//        return UnderlineClassifier.GetOrCreateSingelton(_classificationTypeRegistryService, buffer, underlineTagAggregator);
//    }
//}

/// <summary>
/// MEF-Provider (View-Tagger), der pro <see cref="ITextView"/> einen <see cref="UnderlineClassifier"/>
/// bereitstellt. Er erzeugt dazu einen <see cref="ITagAggregator{T}"/> über die
/// <see cref="UnderlineTag"/>s der View und übergibt ihn dem Klassifizierer.
/// </summary>
[Export(typeof(IViewTaggerProvider))]
[ContentType(NavLanguageContentDefinitions.ContentType)]
[TagType(typeof(ClassificationTag))]
sealed class UnderlineClassifierProvider : IViewTaggerProvider {

    readonly IClassificationTypeRegistryService _classificationTypeRegistryService;
    readonly IViewTagAggregatorFactoryService   _aggregatorFactory;
        
    /// <summary>Wird von MEF mit dem Klassifizierungs-Registrierungsdienst und der Tag-Aggregator-Fabrik erzeugt.</summary>
    [ImportingConstructor]
    public UnderlineClassifierProvider(IClassificationTypeRegistryService classificationTypeRegistryService,
                                       IViewTagAggregatorFactoryService aggregatorFactory) {
        _classificationTypeRegistryService = classificationTypeRegistryService;
        _aggregatorFactory                 = aggregatorFactory;
    }

    /// <summary>Erzeugt bzw. liefert den <see cref="UnderlineClassifier"/> für die angegebene View.</summary>
    public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag {
        var underlineTagAggregator = _aggregatorFactory.CreateTagAggregator<UnderlineTag>(textView);
        return UnderlineClassifier.GetOrCreateSingelton<T>(_classificationTypeRegistryService, textView, buffer, underlineTagAggregator);
    }
}