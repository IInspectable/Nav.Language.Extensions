#region Using Directives

using System;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Commands; 

partial class CommandTarget: IOleCommandTarget {

    /// <summary>
    /// <see cref="IOleCommandTarget"/>-Einsprungpunkt zum Ausführen eines Kommandos. Ermittelt den
    /// Puffer unter dem Cursor und verzweigt anhand der Kommando-Gruppe an
    /// <see cref="ExecuteVisualStudio97"/> bzw. <see cref="ExecuteVisualStudio2000"/>; ohne passenden
    /// Puffer oder Gruppe geht das Kommando an <see cref="NextCommandTarget"/>. Läuft auf dem UI-Thread.
    /// </summary>
    public virtual int Exec(ref Guid pguidCmdGroup, uint commandId, uint executeInformation, IntPtr pvaIn, IntPtr pvaOut) {

        ThreadHelper.ThrowIfNotOnUIThread();

        var subjectBuffer = GetSubjectBufferContainingCaret();

        if (subjectBuffer == null) {
            return NextCommandTarget.Exec(ref pguidCmdGroup, commandId, executeInformation, pvaIn, pvaOut);
        }

        var contentType = subjectBuffer.ContentType;

        if (pguidCmdGroup == VSConstants.GUID_VSStandardCommandSet97) {
            return ExecuteVisualStudio97(ref pguidCmdGroup, commandId, executeInformation, pvaIn, pvaOut, subjectBuffer, contentType);
        }

        if (pguidCmdGroup == VSConstants.VSStd2K) {
            return ExecuteVisualStudio2000(ref pguidCmdGroup, commandId, executeInformation, pvaIn, pvaOut, subjectBuffer, contentType);
        }

        return NextCommandTarget.Exec(ref pguidCmdGroup, commandId, executeInformation, pvaIn, pvaOut);
    }

    /// <summary>
    /// Führt Kommandos der VS-97-Standard-Kommandogruppe aus. Aktuell wird nur <c>ViewCode</c>
    /// behandelt (über <see cref="ExecuteViewCode"/>); alle übrigen gehen an
    /// <see cref="NextCommandTarget"/>.
    /// </summary>
    private int ExecuteVisualStudio97(ref Guid pguidCmdGroup, uint commandId, uint executeInformation, IntPtr pvaIn, IntPtr pvaOut, ITextBuffer subjectBuffer, IContentType contentType) {

        ThreadHelper.ThrowIfNotOnUIThread();

        int result       = VSConstants.S_OK;
        var guidCmdGroup = pguidCmdGroup;

        switch ((VSConstants.VSStd97CmdID) commandId) {

            case VSConstants.VSStd97CmdID.ViewCode:
                ExecuteViewCode(subjectBuffer, contentType, ExecuteNextCommandTarget);
                break;
            default:
                return NextCommandTarget.Exec(ref pguidCmdGroup, commandId, executeInformation, pvaIn, pvaOut);
        }

        return result;

        void ExecuteNextCommandTarget() {
            result = NextCommandTarget.Exec(ref guidCmdGroup, commandId, executeInformation, pvaIn, pvaOut);
        }
    }

    /// <summary>
    /// Führt Kommandos der VS-2000-Standard-Kommandogruppe (<c>VSStd2K</c>) aus. Die Basis-Implementierung
    /// behandelt keines und reicht alle an <see cref="NextCommandTarget"/> weiter; überschreibbar, um
    /// weitere Kommandos dieser Gruppe zu bedienen.
    /// </summary>
    protected virtual int ExecuteVisualStudio2000(ref Guid pguidCmdGroup, uint commandId, uint executeInformation, IntPtr pvaIn, IntPtr pvaOut, ITextBuffer subjectBuffer, IContentType contentType) {

        ThreadHelper.ThrowIfNotOnUIThread();

        // ReSharper disable once RedundantAssignment
        int result       = VSConstants.S_OK;
        var guidCmdGroup = pguidCmdGroup;

        switch ((VSConstants.VSStd2KCmdID) commandId) {

            default:
                ExecuteNextCommandTarget();
                break;
        }

        return result;

        void ExecuteNextCommandTarget() {
            result = NextCommandTarget.Exec(ref guidCmdGroup, commandId, executeInformation, pvaIn, pvaOut);
        }
    }

    /// <summary>
    /// Verteilt das „View Code"-Kommando über den <see cref="HandlerService"/> an die zuständigen
    /// Handler; als Rückfall (kein Handler springt) dient <paramref name="executeNextCommandTarget"/>.
    /// </summary>
    /// <param name="subjectBuffer">Der Puffer unter dem Cursor.</param>
    /// <param name="contentType">Der Content-Type des Puffers.</param>
    /// <param name="executeNextCommandTarget">Rückfall-Aktion, die an <see cref="NextCommandTarget"/> weiterreicht.</param>
    protected void ExecuteViewCode(ITextBuffer subjectBuffer, IContentType contentType, Action executeNextCommandTarget) {
        HandlerService.Execute(
            args: new ViewCodeCommandArgs(WpfTextView, subjectBuffer),
            lastHandler: executeNextCommandTarget);
    }

}