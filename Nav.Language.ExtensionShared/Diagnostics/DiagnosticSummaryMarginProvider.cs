#region Using Directives

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Diagnostics; 

[Export(typeof(IWpfTextViewMarginProvider))]
[Order(After  = "SplitterControl")]
[Order(Before = PredefinedMarginNames.VerticalScrollBarContainer)]
[MarginContainer(PredefinedMarginNames.RightControl)]
[Name(DiagnosticSummaryMargin.MarginName)]
// "Editable" (nicht "Interactive"): Die Diagnose-UI gehört nur in den echten Dokument-Editor.
// Read-only-Ansichten (Annotate/Blame, Diff/Vergleich, History) tragen diese Rolle nicht — dort
// produziert der DiagnosticErrorTagger ohnehin keine Diagnosen (kein Dateipfad), sodass das
// Summary-Icon sinnlos wäre.
[TextViewRole(PredefinedTextViewRoles.Editable)]
[ContentType(NavLanguageContentDefinitions.ContentType)]
class DiagnosticSummaryMarginProvider: IWpfTextViewMarginProvider {

    #region Documentation

    /// <summary>
    /// Creates an <see cref="IWpfTextViewMargin"/> for the given <see cref="IWpfTextViewHost"/>.
    /// </summary>
    /// <param name="wpfTextViewHost">The <see cref="IWpfTextViewHost"/> for which to create the <see cref="IWpfTextViewMargin"/>.</param>
    /// <param name="marginContainer">The margin that will contain the newly-created margin.</param>
    /// <returns>The <see cref="IWpfTextViewMargin"/>.
    /// The value may be null if this <see cref="IWpfTextViewMarginProvider"/> does not participate for this context.
    /// </returns>

    #endregion

    public IWpfTextViewMargin CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer) {
        return new DiagnosticSummaryMargin(wpfTextViewHost.TextView);
    }

}