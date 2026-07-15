#region Using Directives

using System.ComponentModel.Composition;
using System.Windows;

using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

using Pharmatechnik.Nav.Language.Extension;
using Pharmatechnik.Nav.Language.Extension.CodeFixes;

#endregion

namespace Pharmatechnik.Language.Nav.Extension.Margin;

/// <summary>
/// MEF-Provider, der die <see cref="NavMargin"/>-Randleiste für <c>.nav</c>-Ansichten bereitstellt und
/// über die Ordnungs-Attribute im unteren Randbereich (zwischen Zoom-Steuerung und Datei-Health-Indikator)
/// platziert.
/// </summary>
[Export(typeof(IWpfTextViewMarginProvider))]
[Name(NavMargin.MarginName)]
[Order(After = PredefinedMarginNames.ZoomControl)]
[Order(Before = PredefinedMarginNames.FileHealthIndicator)]
[Order(Before = PredefinedMarginNames.HorizontalScrollBarContainer)]
[MarginContainer(PredefinedMarginNames.BottomControl)]
[ContentType(NavLanguageContentDefinitions.ContentType)]
[TextViewRole(PredefinedTextViewRoles.Document)]
[GridUnitType(GridUnitType.Auto)]
[GridCellLength(1.0)]
sealed class NavMarginProvider: IWpfTextViewMarginProvider {

    readonly ITextChangeService _textChangeService;

    /// <summary>Initialisiert den Provider mit dem per MEF importierten <paramref name="textChangeService"/>.</summary>
    [ImportingConstructor]
    public NavMarginProvider(ITextChangeService textChangeService) {
        _textChangeService = textChangeService;
    }

    /// <summary>
    /// Creates an <see cref="IWpfTextViewMargin"/> for the given <see cref="IWpfTextViewHost"/>.
    /// </summary>
    /// <param name="wpfTextViewHost">The <see cref="IWpfTextViewHost"/> for which to create the <see cref="IWpfTextViewMargin"/>.</param>
    /// <param name="marginContainer">The margin that will contain the newly-created margin.</param>
    /// <returns>The <see cref="IWpfTextViewMargin"/>.
    /// The value may be null if this <see cref="IWpfTextViewMarginProvider"/> does not participate for this context.
    /// </returns>
    public IWpfTextViewMargin CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer) {
        return new NavMargin(wpfTextViewHost.TextView, _textChangeService);
    }

}