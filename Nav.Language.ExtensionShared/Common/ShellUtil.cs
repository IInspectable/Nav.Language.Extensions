#region Using Directives

using System;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Common; 

/// <summary>
/// Kleine Hilfsschicht, um dem Benutzer über die Visual-Studio-Shell (<see cref="IVsUIShell"/>)
/// modale Meldungen anzuzeigen. Alle Aufrufe erwarten den UI-Thread.
/// </summary>
static class ShellUtil {

    /// <summary>
    /// Zeigt <paramref name="message"/> als Informations-Meldung (Info-Symbol) an. Muss auf dem
    /// UI-Thread aufgerufen werden.
    /// </summary>
    /// <param name="message">Der anzuzeigende Meldungstext.</param>
    public static void ShowInfoMessage(string message) {
        ThreadHelper.ThrowIfNotOnUIThread();
        ShowMessagebox(message, OLEMSGICON.OLEMSGICON_INFO);
    }

    /// <summary>
    /// Zeigt <paramref name="message"/> als Fehler-Meldung (kritisches Symbol) an. Muss auf dem
    /// UI-Thread aufgerufen werden.
    /// </summary>
    /// <param name="message">Der anzuzeigende Meldungstext.</param>
    public static void ShowErrorMessage(string message) {      
        ThreadHelper.ThrowIfNotOnUIThread();
        ShowMessagebox(message, OLEMSGICON.OLEMSGICON_CRITICAL);
    }

    /// <summary>
    /// Zeigt eine Meldungsbox über <see cref="IVsUIShell.ShowMessageBox"/> an. Gemeinsame
    /// Implementierung hinter <see cref="ShowInfoMessage"/> und <see cref="ShowErrorMessage"/>.
    /// </summary>
    /// <param name="message">Der anzuzeigende Meldungstext.</param>
    /// <param name="msgicon">Das darzustellende Symbol (Info, kritisch, …).</param>
    /// <param name="msgbtn">Die anzubietenden Schaltflächen; Vorgabe <c>OK</c>.</param>
    /// <param name="msgdefbtn">Die vorausgewählte Standard-Schaltfläche.</param>
    static void ShowMessagebox(string message, OLEMSGICON msgicon, 
                               OLEMSGBUTTON msgbtn = OLEMSGBUTTON.OLEMSGBUTTON_OK, 
                               OLEMSGDEFBUTTON msgdefbtn = OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST) {

        ThreadHelper.ThrowIfNotOnUIThread();

        var uiShell = ServiceProvider.GlobalProvider.GetService(typeof(SVsUIShell)) as IVsUIShell;
        var unused  = Guid.Empty;

        uiShell?.ShowMessageBox(
            dwCompRole     : 0,
            rclsidComp     : ref unused,
            pszTitle       : null,
            pszText        : message,
            pszHelpFile    : null,
            dwHelpContextID: 0,
            msgbtn         : msgbtn,
            msgdefbtn      : msgdefbtn,
            msgicon        : msgicon,
            fSysAlert      : 0,
            pnResult       : out _);
    }
}