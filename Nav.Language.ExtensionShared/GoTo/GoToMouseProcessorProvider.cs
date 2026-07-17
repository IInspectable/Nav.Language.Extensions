#region Using Directives

using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;

using Pharmatechnik.Nav.Language.Extension.Common;
using Pharmatechnik.Nav.Language.Extension.GoToLocation;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.GoTo; 

/// <summary>
/// Der MEF-Provider, der je editierbarer Nav-Editor-Sicht den <see cref="GoToMouseProcessor"/> beisteuert
/// (Inhaltstyp <see cref="NavLanguageContentDefinitions.ContentType"/>). Reicht die per Import bezogenen
/// Dienste durch: den <see cref="TextViewConnectionListener"/>, die
/// <see cref="IViewTagAggregatorFactoryService"/> (zum Abfragen der <see cref="GoToTag"/>-Tags unter der
/// Maus) und den <see cref="GoToLocationService"/> (führt den eigentlichen Sprung aus).
/// </summary>
[Export(typeof(IMouseProcessorProvider))]
[Name("Nav/" + nameof(GoToMouseProcessorProvider))]
[ContentType(NavLanguageContentDefinitions.ContentType)]
[TextViewRole(PredefinedTextViewRoles.Editable)]
sealed class GoToMouseProcessorProvider : IMouseProcessorProvider {

    readonly TextViewConnectionListener       _textViewConnectionListener;
    readonly IViewTagAggregatorFactoryService _viewTagAggregatorFactoryService;
    readonly GoToLocationService              _goToLocationService;

    /// <summary>Bezieht die benötigten Dienste per MEF-Import.</summary>
    [ImportingConstructor]
    public GoToMouseProcessorProvider(TextViewConnectionListener textViewConnectionListener,
                                      IViewTagAggregatorFactoryService viewTagAggregatorFactoryService,
                                      GoToLocationService goToLocationService) {

        _textViewConnectionListener      = textViewConnectionListener;
        _viewTagAggregatorFactoryService = viewTagAggregatorFactoryService;
        _goToLocationService             = goToLocationService;
    }

    /// <summary>Liefert den <see cref="GoToMouseProcessor"/> (Singleton) für <paramref name="textView"/>.</summary>
    public IMouseProcessor GetAssociatedProcessor(IWpfTextView textView) {
        return GoToMouseProcessor.GetMouseProcessorForView(textView, _textViewConnectionListener, _viewTagAggregatorFactoryService, _goToLocationService);
    }
}
