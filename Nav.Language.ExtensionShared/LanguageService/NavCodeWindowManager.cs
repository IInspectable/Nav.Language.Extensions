#region Using Directives

using System;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.TextManager.Interop;

using Pharmatechnik.Nav.Utilities.Logging;
using Pharmatechnik.Nav.Language.Extension.Common;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.LanguageService; 

/// <summary>
/// <see cref="IVsCodeWindowManager"/> des Nav-Language-Service: verwaltet je Code-Fenster die
/// Navigationsleiste (Dropdown-Bar) über einer Nav-Datei. Blendet die <see cref="NavigationBar.NavigationBar"/>
/// abhängig von <see cref="NavLanguagePreferences"/>.<c>ShowNavigationBar</c> ein bzw. aus und reagiert auf
/// Änderungen dieser Einstellung. Wird von <see cref="NavLanguageService.GetCodeWindowManager"/> erzeugt.
/// </summary>
class NavCodeWindowManager: IVsCodeWindowManager {

    static readonly Logger Logger = Logger.Create<NavCodeWindowManager>();

    readonly IVsCodeWindow    _codeWindow;
    readonly IServiceProvider _serviceProvider;

    NavigationBar.NavigationBar _navigationBar;

    /// <summary>
    /// Erzeugt den Manager für ein Code-Fenster.
    /// </summary>
    /// <param name="languageService">Der zugehörige Nav-Language-Service (Quelle der Einstellungen).</param>
    /// <param name="serviceProvider">VS-Service-Provider zum Auflösen von Editor-Diensten.</param>
    /// <param name="codeWindow">Das zu verwaltende Code-Fenster.</param>
    public NavCodeWindowManager(NavLanguageService languageService, IServiceProvider serviceProvider, IVsCodeWindow codeWindow) {
        LanguageService  = languageService;
        _codeWindow      = codeWindow;
        _serviceProvider = serviceProvider;
    }

    /// <summary>Der zugehörige Nav-Language-Service (liefert u.a. die Editor-Einstellungen).</summary>
    public NavLanguageService LanguageService { get; }

    /// <summary>
    /// Wird bei jeder neuen Text-Ansicht des Code-Fensters aufgerufen; die Nav-Umsetzung ist ein No-op.
    /// </summary>
    /// <param name="pView">Die neue Text-Ansicht.</param>
    public int OnNewView(IVsTextView pView) {
        return VSConstants.S_OK;
    }

    /// <summary>
    /// Fügt die Navigationsleiste hinzu (falls per Einstellung gewünscht) und abonniert Änderungen der
    /// Editor-Einstellungen. Wird von VS beim Öffnen des Code-Fensters aufgerufen.
    /// </summary>
    public int AddAdornments() {

        AddOrRemoveDropdown(showNavigationBar: LanguageService.Preferences.ShowNavigationBar);

        LanguageService.Preferences.Changed += OnPreferencesChanged;

        return VSConstants.S_OK;
    }

    /// <summary>
    /// Entfernt die Navigationsleiste und meldet sich von den Einstellungs-Änderungen ab. Wird von VS
    /// beim Schließen des Code-Fensters aufgerufen.
    /// </summary>
    public int RemoveAdornments() {

        AddOrRemoveDropdown(showNavigationBar: false);

        LanguageService.Preferences.Changed -= OnPreferencesChanged;

        return VSConstants.S_OK;
    }

    /// <summary>
    /// Reagiert auf geänderte Editor-Einstellungen und blendet die Navigationsleiste entsprechend
    /// <see cref="NavLanguagePreferences"/>.<c>ShowNavigationBar</c> ein oder aus.
    /// </summary>
    private void OnPreferencesChanged(object sender, EventArgs e) {
        AddOrRemoveDropdown(LanguageService.Preferences.ShowNavigationBar);
    }

    /// <summary>
    /// Fügt die Navigationsleiste hinzu bzw. entfernt sie und vermeidet dabei doppelte oder fremde
    /// Dropdown-Bars im Code-Fenster.
    /// </summary>
    /// <param name="showNavigationBar">Ob die Navigationsleiste angezeigt werden soll.</param>
    void AddOrRemoveDropdown(bool showNavigationBar) {

        // ReSharper disable once SuspiciousTypeConversion.Global
        if (!(_codeWindow is IVsDropdownBarManager dropdownManager)) {
            return;
        }

        if (showNavigationBar) {
            var existingDropdownBar = GetDropdownBar(dropdownManager);
            if (existingDropdownBar != null) {

                // Check if the existing dropdown is already one of ours, and do nothing if it is.
                if (_navigationBar != null &&
                    _navigationBar == GetDropdownBarClient(existingDropdownBar)) {
                    return;
                }

                // Not ours, so remove the old one so that we can add ours.
                RemoveDropdownBar(dropdownManager);
            }

            AddDropdownBar(dropdownManager);
        } else {
            RemoveDropdownBar(dropdownManager);
        }
    }

    /// <summary>
    /// Erzeugt die <see cref="NavigationBar.NavigationBar"/> zur Primär-Ansicht des Code-Fensters und
    /// registriert sie als Dropdown-Bar-Client. Bricht mit Log-Warnung ab, wenn keine Ansicht bzw. kein
    /// <c>IWpfTextView</c> ermittelbar ist.
    /// </summary>
    /// <param name="dropdownManager">Der Dropdown-Bar-Manager des Code-Fensters.</param>
    void AddDropdownBar(IVsDropdownBarManager dropdownManager) {

        _codeWindow.GetPrimaryView(out var textView);

        if (textView == null) {
            Logger.Warn($"{nameof(AddDropdownBar)}: Unable to get primary view");
            return;
        }

        var editorAdaptersFactoryService = _serviceProvider.GetMefService<IVsEditorAdaptersFactoryService>();

        var wpfTextView = editorAdaptersFactoryService.GetWpfTextView(textView);
        if (wpfTextView == null) {
            Logger.Warn($"{nameof(AddDropdownBar)}: Unable to get IWpfTextView");
            return;
        }

        var dropdownBarClient = new NavigationBar.NavigationBar(wpfTextView.TextBuffer, dropdownManager, _codeWindow, _serviceProvider);

        #if ShowMemberCombobox
            var hr = dropdownManager.AddDropdownBar(cCombos: 3, pClient: dropdownBarClient);
        #else
        var hr = dropdownManager.AddDropdownBar(cCombos: 2, pClient: dropdownBarClient);
        #endif
        if (ErrorHandler.Failed(hr)) {
            ErrorHandler.ThrowOnFailure(hr);
        }

        _navigationBar = dropdownBarClient;
    }

    /// <summary>
    /// Entfernt die Dropdown-Bar des Code-Fensters und gibt die <see cref="NavigationBar.NavigationBar"/> frei.
    /// </summary>
    /// <param name="dropdownManager">Der Dropdown-Bar-Manager des Code-Fensters.</param>
    void RemoveDropdownBar(IVsDropdownBarManager dropdownManager) {
        dropdownManager.RemoveDropdownBar();

        _navigationBar?.Dispose();
        _navigationBar = null;
    }

    /// <summary>
    /// Liefert die aktuell im Code-Fenster registrierte Dropdown-Bar (oder <c>null</c>).
    /// </summary>
    /// <param name="dropdownManager">Der Dropdown-Bar-Manager des Code-Fensters.</param>
    static IVsDropdownBar GetDropdownBar(IVsDropdownBarManager dropdownManager) {
        ErrorHandler.ThrowOnFailure(dropdownManager.GetDropdownBar(out var existingDropdownBar));
        return existingDropdownBar;
    }

    /// <summary>
    /// Liefert den Client einer Dropdown-Bar — dient dem Abgleich, ob die vorhandene Bar bereits unsere
    /// <see cref="NavigationBar.NavigationBar"/> ist.
    /// </summary>
    /// <param name="dropdownBar">Die zu befragende Dropdown-Bar.</param>
    static IVsDropdownBarClient GetDropdownBarClient(IVsDropdownBar dropdownBar) {
        ErrorHandler.ThrowOnFailure(dropdownBar.GetClient(out var dropdownBarClient));
        return dropdownBarClient;
    }

}