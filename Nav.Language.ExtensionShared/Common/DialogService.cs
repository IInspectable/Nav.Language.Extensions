#region Using Directives

using System;
using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Imaging.Interop;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Common; 

/// <summary>
/// Dienst zum Anzeigen einfacher modaler Eingabedialoge in Visual Studio.
/// </summary>
interface IDialogService {

    /// <summary>
    /// Zeigt einen modalen Eingabedialog mit einem Textfeld an.
    /// </summary>
    /// <param name="promptText">Der über dem Eingabefeld angezeigte Aufforderungstext.</param>
    /// <param name="title">Der Fenstertitel; <c>null</c> für den Standardtitel.</param>
    /// <param name="defaultResonse">Der beim Öffnen vorbelegte Text.</param>
    /// <param name="iconMoniker">Symbol neben dem Aufforderungstext.</param>
    /// <param name="validator">
    /// Optionale Prüffunktion, die zur Eingabe eine Fehlermeldung (oder <c>null</c> bei Gültigkeit)
    /// liefert.
    /// </param>
    /// <param name="noteIconMoniker">Symbol neben dem Hinweistext.</param>
    /// <param name="note">Optionaler Hinweistext unterhalb des Eingabefelds.</param>
    /// <returns>Der eingegebene Text oder <c>null</c>, wenn der Dialog abgebrochen wurde.</returns>
    string ShowInputDialog(string promptText, string title = null, string defaultResonse = null, 
                           ImageMoniker iconMoniker = default, Func<string, string> validator = null,
                           ImageMoniker noteIconMoniker = default, string note = null);
}

/// <summary>
/// MEF-Implementierung von <see cref="IDialogService"/>, die den Eingabedialog über
/// <see cref="InputDialog"/> und <see cref="InputDialogViewModel"/> realisiert.
/// </summary>
[Export(typeof(IDialogService))]
class DialogService: IDialogService {

    /// <inheritdoc/>
    public string ShowInputDialog(string promptText, string title = null, string defaultResonse = null, 
                                  ImageMoniker iconMoniker = new(), Func<string, string> validator = null,
                                  ImageMoniker noteIconMoniker = default, string note = null) {

        var viewModel = new InputDialogViewModel {
            PromptText      = promptText,
            Title           = title,
            Text            = defaultResonse,
            IconMoniker     = iconMoniker,
            Validator       = validator,
            Note            = note,
            NoteIconMoniker = noteIconMoniker
        };

        var dlg = new InputDialog(viewModel);
        if (dlg.ShowModal() == false) {
            return null;
        }

        return viewModel.Text;
    }
}