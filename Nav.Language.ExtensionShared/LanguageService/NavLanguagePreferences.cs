#region Using Directives

using System;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Package;
using Microsoft.VisualStudio.TextManager.Interop;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.LanguageService; 

/// <summary>
/// Editor-Einstellungen des klassischen Nav-Language-Service (abgeleitet von <see cref="LanguagePreferences"/>).
/// Setzt im Konstruktor die Nav-spezifischen Vorgabewerte (Klammer-Matching, Auto-Outlining,
/// Navigationsleiste, Einrückung usw.) und meldet über <see cref="Changed"/> Benutzeränderungen, auf die
/// z.B. der <see cref="NavCodeWindowManager"/> reagiert.
/// </summary>
public class NavLanguagePreferences: LanguagePreferences {

    /// <summary>
    /// Erzeugt die Einstellungen und belegt sie mit den Nav-Vorgabewerten.
    /// </summary>
    /// <param name="site">Der VS-Service-Provider.</param>
    /// <param name="langSvc">GUID des Language-Service.</param>
    /// <param name="name">Name des Language-Service.</param>
    public NavLanguagePreferences(IServiceProvider site, Guid langSvc, string name)
        : base(site, langSvc, name) {

        EnableCodeSense             = true;
        EnableMatchBraces           = true;
        EnableMatchBracesAtCaret    = true;
        EnableShowMatchingBrace     = true;
        EnableCommenting            = true;
        HighlightMatchingBraceFlags = _HighlightMatchingBraceFlags.HMB_USERECTANGLEBRACES;
        LineNumbers                 = true;
        MaxErrorMessages            = 100;
        AutoOutlining               = true;
        MaxRegionTime               = 2000;
        InsertTabs                  = false;
        IndentSize                  = 4;
        ShowNavigationBar           = true;
        EnableAsyncCompletion       = true;
        WordWrap                    = false;
        WordWrapGlyphs              = true;
        AutoListMembers             = true;
        EnableQuickInfo             = true;
        ParameterInformation        = true;
        HideAdvancedMembers         = false;
    }

    /// <summary>Wird ausgelöst, wenn der Benutzer die Editor-Einstellungen ändert.</summary>
    public event EventHandler Changed;

    /// <summary>
    /// Übernimmt geänderte Benutzer-Einstellungen (Basisverhalten) und löst anschließend
    /// <see cref="Changed"/> aus.
    /// </summary>
    /// <param name="viewPrefs">Geänderte Ansichts-Einstellungen.</param>
    /// <param name="framePrefs">Geänderte Fenster-Einstellungen.</param>
    /// <param name="langPrefs">Geänderte Sprach-Einstellungen.</param>
    /// <param name="fontColorPrefs">Geänderte Schriftart-/Farb-Einstellungen.</param>
    public override int OnUserPreferencesChanged2(VIEWPREFERENCES2[] viewPrefs, FRAMEPREFERENCES2[] framePrefs, LANGPREFERENCES2[] langPrefs, FONTCOLORPREFERENCES2[] fontColorPrefs) {

        base.OnUserPreferencesChanged2(viewPrefs, framePrefs, langPrefs, fontColorPrefs);

        Changed?.Invoke(this, EventArgs.Empty);

        return VSConstants.S_OK;
    }

}