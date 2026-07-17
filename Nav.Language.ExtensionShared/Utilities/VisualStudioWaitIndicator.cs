#region Using Directives

using System;
using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Utilities; 

/// <summary>
/// Visual-Studio-Umsetzung von <see cref="IWaitIndicator"/>; als MEF-Dienst exportiert. Startet je
/// Operation einen <see cref="VisualStudioWaitContext"/> (Threaded Wait Dialog) und übersetzt einen
/// Abbruch in <see cref="WaitIndicatorResult.Canceled"/>.
/// </summary>
[Export(typeof(IWaitIndicator))]
sealed class VisualStudioWaitIndicator : IWaitIndicator {

    readonly SVsServiceProvider _serviceProvider;

    /// <summary>
    /// MEF-Import-Konstruktor; hält den VS-Service-Provider zum Auflösen der Dialog-Factory.
    /// </summary>
    /// <param name="serviceProvider">Der von MEF bereitgestellte VS-Service-Provider.</param>
    [ImportingConstructor]
    public VisualStudioWaitIndicator(SVsServiceProvider serviceProvider) {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public WaitIndicatorResult Wait(string title, string message, bool allowCancel, Action<IWaitContext> action) {

        ThreadHelper.ThrowIfNotOnUIThread();

        using var waitContext = StartWait(title, message, allowCancel);

        try {
            action(waitContext);

            return WaitIndicatorResult.Completed;
        } catch(OperationCanceledException) {
            return WaitIndicatorResult.Canceled;
        } catch(AggregateException e) {
            if(e.InnerExceptions[0] is OperationCanceledException) {
                return WaitIndicatorResult.Canceled;
            } else {
                throw;
            }
        }

    }

    /// <summary>
    /// Erzeugt und öffnet einen <see cref="VisualStudioWaitContext"/> über die
    /// <see cref="IVsThreadedWaitDialogFactory"/> des VS-Service-Providers. Muss auf dem UI-Thread
    /// aufgerufen werden.
    /// </summary>
    /// <param name="title">Titel des Wait-Dialogs.</param>
    /// <param name="message">Anfänglicher Meldungstext.</param>
    /// <param name="allowCancel">Ob dem Benutzer ein Abbrechen angeboten wird.</param>
    VisualStudioWaitContext StartWait(string title, string message, bool allowCancel) {
            
        ThreadHelper.ThrowIfNotOnUIThread();
           
        var dialogFactory = (IVsThreadedWaitDialogFactory) _serviceProvider.GetService(typeof(SVsThreadedWaitDialogFactory));
           

        return new VisualStudioWaitContext(dialogFactory, title, message, allowCancel);
    }

    /// <inheritdoc/>
    IWaitContext IWaitIndicator.StartWait(string title, string message, bool allowCancel) {
        ThreadHelper.ThrowIfNotOnUIThread();
        return StartWait(title, message, allowCancel);
    }
}