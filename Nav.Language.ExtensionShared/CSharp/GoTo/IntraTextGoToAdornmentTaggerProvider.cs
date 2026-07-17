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

/// <summary>
/// MEF-Provider (<see cref="IViewTaggerProvider"/>), der in C#-Editoren die sichtbare Adornment-Schicht
/// der Nav-GoTo-Symbole liefert: Er erzeugt je <see cref="ITextView"/> einen
/// <see cref="IntraTextGoToAdornmentTagger"/>, der die vom <see cref="IntraTextGoToTaggerProvider"/>
/// berechneten <see cref="IntraTextGoToTag"/>-Datentags (über einen
/// <see cref="ITagAggregator{T}"/>) in klickbare <see cref="IntraTextAdornmentTag"/>-Symbole umsetzt.
/// </summary>
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

    /// <summary>
    /// Liefert den view-gebundenen Adornment-Tagger — jedoch nur für den Haupt-<see cref="ITextBuffer"/>
    /// der <paramref name="textView"/> (bei projizierten Puffern <c>null</c>) und nur, wenn der
    /// angeforderte Tag-Typ <typeparamref name="T"/> mit <see cref="IntraTextAdornmentTag"/> kompatibel ist.
    /// </summary>
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