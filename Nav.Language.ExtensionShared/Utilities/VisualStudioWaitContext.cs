#region Using Directives

using System.Threading;

using Microsoft.VisualStudio.Shell;

using System.Runtime.InteropServices;

using Microsoft.VisualStudio.Shell.Interop;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Utilities; 

/// <summary>
/// Visual-Studio-Umsetzung von <see cref="IWaitContext"/> auf Basis des „Threaded Wait Dialog"
/// (<see cref="IVsThreadedWaitDialog3"/>). Zeigt für die Dauer einer Operation einen Warte-Dialog an,
/// aktualisiert dessen Meldung und meldet ein benutzerseitiges Abbrechen über den
/// <see cref="System.Threading.CancellationToken"/>. Wird vom <see cref="VisualStudioWaitIndicator"/>
/// erzeugt.
/// </summary>
sealed partial class VisualStudioWaitContext: IWaitContext {

    const int DelayToShowDialogSecs = 1;

    readonly IVsThreadedWaitDialog3  _dialog;
    readonly CancellationTokenSource _cancellationTokenSource;
    readonly string                  _title;

    string _message;
    bool   _allowCancel;

    /// <summary>
    /// Erzeugt den Warte-Kontext und öffnet den zugehörigen Wait-Dialog. Muss auf dem UI-Thread
    /// aufgerufen werden.
    /// </summary>
    /// <param name="dialogFactory">Factory für den <see cref="IVsThreadedWaitDialog3"/>.</param>
    /// <param name="title">Titel des Dialogs.</param>
    /// <param name="message">Anfänglicher Meldungstext.</param>
    /// <param name="allowCancel">Ob dem Benutzer ein Abbrechen angeboten wird.</param>
    public VisualStudioWaitContext(IVsThreadedWaitDialogFactory dialogFactory,
                                   string title,
                                   string message,
                                   bool allowCancel) {

        ThreadHelper.ThrowIfNotOnUIThread();

        _title                   = title;
        _message                 = message;
        _allowCancel             = allowCancel;
        _cancellationTokenSource = new CancellationTokenSource();
        _dialog                  = CreateDialog(dialogFactory);
    }

    /// <summary>
    /// Erzeugt den <see cref="IVsThreadedWaitDialog3"/>, startet ihn mit dem <see cref="Callback"/> und
    /// gibt ihn zurück. Muss auf dem UI-Thread aufgerufen werden.
    /// </summary>
    /// <param name="dialogFactory">Factory für den Wait-Dialog.</param>
    IVsThreadedWaitDialog3 CreateDialog(IVsThreadedWaitDialogFactory dialogFactory) {

        ThreadHelper.ThrowIfNotOnUIThread();

        Marshal.ThrowExceptionForHR(dialogFactory.CreateInstance(out var dialog2));

        var dialog3 = (IVsThreadedWaitDialog3) dialog2;

        var callback = new Callback(this);

        dialog3.StartWaitDialogWithCallback(
            szWaitCaption: _title,
            szWaitMessage: _message,
            szProgressText: null,
            varStatusBmpAnim: null,
            szStatusBarText: null,
            fIsCancelable: _allowCancel,
            iDelayToShowDialog: DelayToShowDialogSecs,
            fShowProgress: false,
            iTotalSteps: 0,
            iCurrentStep: 0,
            pCallback: callback);

        return dialog3;
    }

    /// <inheritdoc/>
    public CancellationToken CancellationToken {
        get {
            return _allowCancel
                ? _cancellationTokenSource.Token
                : CancellationToken.None;
        }
    }

    /// <inheritdoc/>
    public string Message {
        get { return _message; }
        set {
            _message = value;
            UpdateDialog();
        }
    }

    /// <inheritdoc/>
    public bool AllowCancel {
        get { return _allowCancel; }
        set {
            _allowCancel = value;
            UpdateDialog();
        }
    }

    /// <summary>
    /// Überträgt den aktuellen <see cref="Message"/>-/<see cref="AllowCancel"/>-Stand auf den offenen
    /// Wait-Dialog. Wechselt dafür auf den UI-Thread.
    /// </summary>
    void UpdateDialog() {

        NavLanguagePackage.Jtf.RunAsync(async () => {

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            _dialog.UpdateProgress(
                szUpdatedWaitMessage: _message,
                szProgressText: null,
                szStatusBarText: null,
                iCurrentStep: 0,
                iTotalSteps: 0,
                fDisableCancel: !_allowCancel,
                pfCanceled: out bool _);
        }).FileAndForget("nav/waitcontext/updatedialog");
    }

    /// <inheritdoc/>
    public void UpdateProgress() {
    }

    /// <summary>
    /// Schließt den Wait-Dialog. Muss auf dem UI-Thread aufgerufen werden.
    /// </summary>
    public void Dispose() {

        ThreadHelper.ThrowIfNotOnUIThread();

        _dialog.EndWaitDialog(out int _);
    }

    /// <summary>
    /// Löst bei erlaubtem Abbruch (<see cref="AllowCancel"/>) die Stornierung des
    /// <see cref="CancellationToken"/> aus. Wird vom <see cref="Callback"/> aufgerufen.
    /// </summary>
    void OnCanceled() {
        if (_allowCancel) {
            _cancellationTokenSource.Cancel();
        }
    }

}