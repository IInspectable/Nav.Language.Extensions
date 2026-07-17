#region Using Directives

using System.Runtime.InteropServices;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.TextManager.Interop;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.LanguageService; 

/// <summary>
/// Der klassische (Legacy-)Language-Service der Nav-Sprache (<see cref="IVsLanguageInfo"/>). Meldet
/// Visual Studio Sprachname und Dateiendung, liefert je Code-Fenster einen <see cref="NavCodeWindowManager"/>
/// und hält die <see cref="NavLanguagePreferences"/>. Die eigentliche Klassifizierung übernimmt der
/// moderne Editor-Stack (MEF-Classifier), daher liefert <see cref="GetColorizer"/> keinen Colorizer.
/// </summary>
[Guid(NavLanguagePackage.Guids.LanguageGuidString)]
public class NavLanguageService: IVsLanguageInfo {

        

    readonly NavLanguagePackage _package;

    private NavLanguagePreferences _preferences;

    /// <summary>
    /// Erzeugt den Language-Service.
    /// </summary>
    /// <param name="package">Das besitzende <see cref="NavLanguagePackage"/> (dient als Service-Provider).</param>
    public NavLanguageService(NavLanguagePackage package) {
        _package = package;
    }

    /// <summary>
    /// Die (verzögert erzeugten) Editor-Einstellungen des Nav-Language-Service.
    /// </summary>
    public NavLanguagePreferences Preferences {
        get {
            if (_preferences == null) {
                _preferences = new NavLanguagePreferences(_package, typeof(NavLanguageService).GUID, NavLanguageContentDefinitions.LanguageName);
                _preferences.Init();
            }

            return _preferences;
        }
    }

    /// <summary>
    /// Liefert je Code-Fenster einen <see cref="NavCodeWindowManager"/>, der u.a. die Navigationsleiste
    /// verwaltet.
    /// </summary>
    /// <param name="pCodeWin">Das Code-Fenster.</param>
    /// <param name="ppCodeWinMgr">Rückgabe: der erzeugte Code-Fenster-Manager.</param>
    public int GetCodeWindowManager(IVsCodeWindow pCodeWin, out IVsCodeWindowManager ppCodeWinMgr) {

        ppCodeWinMgr = new NavCodeWindowManager(this, _package, pCodeWin);

        return VSConstants.S_OK;
    }

    /// <summary>
    /// Liefert bewusst keinen Colorizer (<c>E_NOTIMPL</c>) — die Klassifizierung übernimmt der moderne
    /// MEF-Editor-Stack.
    /// </summary>
    /// <param name="pBuffer">Der Text-Puffer.</param>
    /// <param name="ppColorizer">Rückgabe: stets <c>null</c>.</param>
    public int GetColorizer(IVsTextLines pBuffer, out IVsColorizer ppColorizer) {
        ppColorizer = null;
        return VSConstants.E_NOTIMPL;
    }

    /// <summary>
    /// Liefert die von diesem Language-Service behandelte Dateiendung
    /// (<see cref="NavLanguageContentDefinitions.FileExtension"/>).
    /// </summary>
    /// <param name="pbstrExtensions">Rückgabe: die Dateiendung(en).</param>
    public int GetFileExtensions(out string pbstrExtensions) {
        pbstrExtensions = NavLanguageContentDefinitions.FileExtension;
        return VSConstants.S_OK;
    }

    /// <summary>
    /// Liefert den Anzeigenamen der Sprache (<see cref="NavLanguageContentDefinitions.LanguageName"/>).
    /// </summary>
    /// <param name="bstrName">Rückgabe: der Sprachname.</param>
    public int GetLanguageName(out string bstrName) {
        bstrName = NavLanguageContentDefinitions.LanguageName;
        return VSConstants.S_OK;
    }

}