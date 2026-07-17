#region Using Directives

using System;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Commands; 

partial class CommandTarget : IOleCommandTarget {
        
    /// <summary>
    /// <see cref="IOleCommandTarget"/>-Einsprungpunkt zum Abfragen des Kommando-Zustands (aktiviert,
    /// sichtbar, Anzeigetext). Verzweigt anhand der Kommando-Gruppe an
    /// <see cref="QueryVisualStudio2000Status"/> bzw. <see cref="QueryVisualStudio97Status"/>; unbekannte
    /// Gruppen gehen an <see cref="NextCommandTarget"/>. Läuft auf dem UI-Thread.
    /// </summary>
    public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText) {

        ThreadHelper.ThrowIfNotOnUIThread();

        if (pguidCmdGroup == VSConstants.VSStd2K)
        {
            return QueryVisualStudio2000Status(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        if (pguidCmdGroup == VSConstants.GUID_VSStandardCommandSet97) {
            return QueryVisualStudio97Status(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        return NextCommandTarget.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
    }
        
    /// <summary>
    /// Fragt den Zustand von Kommandos der VS-2000-Gruppe (<c>VSStd2K</c>) ab. Aktuell wird keines
    /// eigens behandelt; alle gehen an <see cref="NextCommandTarget"/>.
    /// </summary>
    int QueryVisualStudio2000Status(ref Guid pguidCmdGroup, uint commandCount, OLECMD[] prgCmds, IntPtr commandText) {
            
        ThreadHelper.ThrowIfNotOnUIThread();

        switch((VSConstants.VSStd2KCmdID) prgCmds[0].cmdID) {
                
            default:
                return NextCommandTarget.QueryStatus(ref pguidCmdGroup, commandCount, prgCmds, commandText);
        }
    }

    /// <summary>
    /// Fragt den Zustand von Kommandos der VS-97-Gruppe ab. Für <c>ViewCode</c> wird über
    /// <see cref="QueryViewCode"/> abgefragt; alle übrigen gehen an <see cref="NextCommandTarget"/>.
    /// </summary>
    private int QueryVisualStudio97Status(ref Guid pguidCmdGroup, uint commandCount, OLECMD[] prgCmds, IntPtr commandText) {
        ThreadHelper.ThrowIfNotOnUIThread();
        switch ((VSConstants.VSStd97CmdID)prgCmds[0].cmdID) {
            case VSConstants.VSStd97CmdID.ViewCode:
                return QueryViewCode(ref pguidCmdGroup, commandCount, prgCmds, commandText);

            default:
                return NextCommandTarget.QueryStatus(ref pguidCmdGroup, commandCount, prgCmds, commandText);
        }
    }
        
    /// <summary>
    /// Fragt den Zustand des „View Code"-Kommandos über die zuständigen Handler ab (via
    /// <see cref="GetCommandState{T}"/> mit <see cref="ViewCodeCommandArgs"/>).
    /// </summary>
    private int QueryViewCode(ref Guid pguidCmdGroup, uint commandCount, OLECMD[] prgCmds, IntPtr commandText) {
        return GetCommandState(
            createArgs   : (v, b) => new ViewCodeCommandArgs(v, b),
            pguidCmdGroup: ref pguidCmdGroup,
            commandCount : commandCount,
            prgCmds      : prgCmds,
            commandText  : commandText);
    }


    /// <summary>
    /// Gemeinsamer Kern der Zustandsabfrage: erzeugt über <paramref name="createArgs"/> die
    /// Kommando-Argumente, fragt über den <see cref="HandlerService"/> den <see cref="CommandState"/>
    /// ab (Rückfall: das nächste Command-Target) und überträgt das Ergebnis — verfügbar/sichtbar,
    /// aktiviert, ggf. Anzeigetext — in die <see cref="OLECMD"/>-Struktur.
    /// </summary>
    /// <typeparam name="T">Der Argument-Typ des abgefragten Kommandos.</typeparam>
    /// <param name="createArgs">Fabrik für die Kommando-Argumente aus Sicht und Puffer.</param>
    /// <param name="pguidCmdGroup">GUID der abgefragten Kommandogruppe.</param>
    /// <param name="commandCount">Anzahl der in <paramref name="prgCmds"/> übergebenen Kommandos.</param>
    /// <param name="prgCmds">Die abzufragenden Kommandos; das jeweilige Statusfeld wird bei Treffer gesetzt.</param>
    /// <param name="commandText">Optionaler Puffer für dynamischen Anzeigetext, sonst <c>IntPtr.Zero</c>.</param>
    int GetCommandState<T>(
        Func<IWpfTextView, ITextBuffer, T> createArgs,
        ref Guid pguidCmdGroup,
        uint commandCount,
        OLECMD[] prgCmds,
        IntPtr commandText)
        where T : CommandArgs {
        var result = VSConstants.S_OK;

        var guidCmdGroup = pguidCmdGroup;

        CommandState ExecuteNextCommandTarget() {
            ThreadHelper.ThrowIfNotOnUIThread();
            result = NextCommandTarget.QueryStatus(ref guidCmdGroup, commandCount, prgCmds, commandText);

            // ReSharper disable BitwiseOperatorOnEnumWithoutFlags
            var isAvailable = ((OLECMDF) prgCmds[0].cmdf & OLECMDF.OLECMDF_ENABLED) == OLECMDF.OLECMDF_ENABLED;
            var isChecked   = ((OLECMDF) prgCmds[0].cmdf & OLECMDF.OLECMDF_LATCHED) == OLECMDF.OLECMDF_LATCHED;
            // ReSharper restore BitwiseOperatorOnEnumWithoutFlags
            return new CommandState(isAvailable, isChecked, GetText(commandText));
        }

        CommandState commandState;
        var          subjectBuffer = GetSubjectBufferContainingCaret();
        if(subjectBuffer == null) {
            commandState = ExecuteNextCommandTarget();
        } else {
            commandState = HandlerService.GetCommandState(
                args       : createArgs(WpfTextView, subjectBuffer),
                lastHandler: ExecuteNextCommandTarget);
        }

        var enabled = commandState.IsAvailable ? OLECMDF.OLECMDF_ENABLED : OLECMDF.OLECMDF_INVISIBLE;
        var latched = commandState.IsChecked   ? OLECMDF.OLECMDF_LATCHED : OLECMDF.OLECMDF_NINCHED;
        // ReSharper disable BitwiseOperatorOnEnumWithoutFlags
        prgCmds[0].cmdf = (uint) (enabled | latched | OLECMDF.OLECMDF_SUPPORTED);
        // ReSharper restore BitwiseOperatorOnEnumWithoutFlags

        if (!string.IsNullOrEmpty(commandState.DisplayText) && GetText(commandText) != commandState.DisplayText) {
            SetText(commandText, commandState.DisplayText);
        }

        return result;
    }

    /// <summary>
    /// Liest den Anzeigetext aus der von Visual Studio übergebenen <c>OLECMDTEXT</c>-Struktur;
    /// liefert <see cref="string.Empty"/>, wenn kein Text vorhanden ist.
    /// </summary>
    static unsafe string GetText(IntPtr pCmdTextInt) {
        if(pCmdTextInt == IntPtr.Zero) {
            return string.Empty;
        }

        OLECMDTEXT* pText = (OLECMDTEXT*) pCmdTextInt;

        // Punt early if there is no text in the structure.
        if(pText->cwActual == 0) {
            return string.Empty;
        }

        // ReSharper disable once RedundantCast ist für vs2019 Build nötig
        return new string((char*)&pText->rgwz, 0, (int) pText->cwActual);
    }

    /// <summary>
    /// Schreibt <paramref name="text"/> in die von Visual Studio bereitgestellte
    /// <c>OLECMDTEXT</c>-Struktur, begrenzt auf deren Pufferlänge und NUL-terminiert.
    /// </summary>
    static unsafe void SetText(IntPtr pCmdTextInt, string text) {
        OLECMDTEXT* pText = (OLECMDTEXT*) pCmdTextInt;

        // If, for some reason, we don't get passed an array, we should just bail
        if(pText->cwBuf == 0) {
            return;
        }

        fixed(char* pinnedText = text) {
            char* src  = pinnedText;
            // ReSharper disable once RedundantCast ist für vs2019 Build nötig
            char* dest = (char*)&pText->rgwz;

            // Don't copy too much, and make sure to reserve space for the terminator
            int length = Math.Min(text.Length, (int) pText->cwBuf - 1);

            for(int i = 0; i < length; i++) {
                *dest++ = *src++;
            }

            // Add terminating NUL
            *dest = '\0';

            pText->cwActual = (uint) length;
        }
    }
}