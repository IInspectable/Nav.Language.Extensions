#region Using Directives

using System;
using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using Pharmatechnik.Nav.Language.Extension.GoToLocation;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.CSharp.GoTo; 

[Export(typeof(IViewTaggerProvider))]
[ContentType("csharp")]
[TagType(typeof(IntraTextAdornmentTag))]
sealed class IntraTextGoToAdornmentTaggerProvider : IViewTaggerProvider {

    readonly IBufferTagAggregatorFactoryService _bufferTagAggregatorFactoryService;
    readonly GoToLocationService                _goToLocationService;

    [ImportingConstructor]
    public IntraTextGoToAdornmentTaggerProvider(IBufferTagAggregatorFactoryService bufferTagAggregatorFactoryService, GoToLocationService goToLocationService) {
        _bufferTagAggregatorFactoryService = bufferTagAggregatorFactoryService;
        _goToLocationService               = goToLocationService;
    }

    public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag {

        if (textView == null) {
            throw new ArgumentNullException(nameof(textView));
        }

        if(buffer == null) {
            throw new ArgumentNullException(nameof(buffer));
        }

        if(buffer != textView.TextBuffer) {
            return null;
        }

        return IntraTextGoToAdornmentTagger.GetTagger(
            view                : (IWpfTextView) textView,
            intraTextGoToTagger : new Lazy<ITagAggregator<IntraTextGoToTag>>( () => _bufferTagAggregatorFactoryService.CreateTagAggregator<IntraTextGoToTag>(textView.TextBuffer)),
            goToLocationService : _goToLocationService) as ITagger<T>;
    }
}