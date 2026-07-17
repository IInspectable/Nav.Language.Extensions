#region Using Directives

using System.ComponentModel.Composition;

using Pharmatechnik.Nav.Language.Extension.Common;
using Pharmatechnik.Nav.Language.Extension.Utilities;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.CodeFixes; 

/// <summary>
/// Der über MEF exportierte, geteilte Dienst-Kontext aller <see cref="CodeFixSuggestedAction"/>en. Er
/// bündelt die von einem Fix beim Anwenden benötigten VS-Editor-Dienste — den Wait-Indicator, den
/// <see cref="ITextChangeService"/> (Anwenden der Edits) und den Dialog-Dienst — und wird von den
/// <see cref="CodeFixSuggestedActionProvider"/>n in die erzeugten Aktionen weitergereicht.
/// </summary>
[Export(typeof(CodeFixSuggestedActionContext))]
class CodeFixSuggestedActionContext {
        
    /// <summary>Importiert die vom Anwenden eines Fixes benötigten Editor-Dienste über MEF.</summary>
    /// <param name="waitIndicator">Zeigt beim Anwenden längerer Fixes eine Warteanzeige.</param>
    /// <param name="textChangeService">Wendet die vom Fix berechneten Edits als widerrufbare Bearbeitung an.</param>
    /// <param name="dialogService">Erlaubt einem Fix, bei Bedarf mit dem Nutzer zu interagieren.</param>
    [ImportingConstructor]
    public CodeFixSuggestedActionContext(IWaitIndicator waitIndicator,
                                         ITextChangeService textChangeService, 
                                         IDialogService dialogService) {

        WaitIndicator     = waitIndicator;
        TextChangeService = textChangeService;
        DialogService     = dialogService;
    }

    /// <summary>Zeigt beim Anwenden längerer Fixes eine Warteanzeige.</summary>
    public IWaitIndicator     WaitIndicator     { get; }
    /// <summary>Wendet die von einem Fix berechneten <see cref="Pharmatechnik.Nav.Language.Text.TextChange"/>s auf den Editor an.</summary>
    public ITextChangeService TextChangeService { get; }
    /// <summary>Dienst für Nutzer-Dialoge, den ein Fix beim Anwenden nutzen kann.</summary>
    public IDialogService     DialogService     { get; }       
}