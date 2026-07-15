#region Using Directives

using System;
using System.Collections;
using System.ComponentModel;

using JetBrains.Annotations;

using Microsoft.VisualStudio.Imaging.Interop;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Common; 

/// <summary>
/// ViewModel des <see cref="InputDialog"/>: hält Aufforderungstext, Titel, Eingabetext, Symbole und
/// Hinweistext und stellt über <see cref="INotifyDataErrorInfo"/> das Ergebnis der Eingabeprüfung
/// bereit.
/// </summary>
sealed class InputDialogViewModel : AbstractNotifyPropertyChanged, INotifyDataErrorInfo {

    /// <summary>
    /// Optionale Prüffunktion, die zum Eingabetext eine Fehlermeldung (oder <c>null</c> bei
    /// Gültigkeit) liefert.
    /// </summary>
    [CanBeNull]
    public Func<string, string> Validator { get; set; }

    private string _promptText;
    /// <summary>Der über dem Eingabefeld angezeigte Aufforderungstext.</summary>
    public string PromptText {
        get => _promptText;
        set => SetProperty(ref _promptText, value);
    }

    private string _title;
    /// <summary>Der Fenstertitel des Dialogs.</summary>
    public string Title {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    private string _text;
    /// <summary>
    /// Der eingegebene Text. Beim Setzen wird der <see cref="Validator"/> ausgewertet und das
    /// Ergebnis in <see cref="TextError"/> abgelegt.
    /// </summary>
    public string Text {
        get => _text;
        set {
            if (SetProperty(ref _text, value)) {
                TextError = Validator?.Invoke(Text);
            }
        }
    }

    private ImageMoniker _iconMoniker;
    /// <summary>Symbol, das neben dem Aufforderungstext angezeigt wird.</summary>
    public ImageMoniker IconMoniker {
        get => _iconMoniker;
        set => SetProperty(ref _iconMoniker, value);
    }

    /// <summary>Gibt an, ob ein Hinweistext (<see cref="Note"/>) vorhanden und anzuzeigen ist.</summary>
    public bool ShouldDisplayImpactText {
        get { return !String.IsNullOrEmpty(Note); }
    }

    private string _note;
    /// <summary>Optionaler Hinweistext unterhalb des Eingabefelds.</summary>
    public string Note {
        get => _note;
        set {
            if (SetProperty(ref _note, value)) {
                // ReSharper disable ExplicitCallerInfoArgument
                NotifyPropertyChanged(nameof(ShouldDisplayImpactText));
            } }
    }

    private ImageMoniker _noteIconMoniker;
    /// <summary>Symbol, das neben dem Hinweistext (<see cref="Note"/>) angezeigt wird.</summary>
    public ImageMoniker NoteIconMoniker {
        get => _noteIconMoniker;
        set => SetProperty(ref _noteIconMoniker, value);
    }

    private string _textError;
    /// <summary>
    /// Die aktuelle Fehlermeldung zur Eingabe oder <c>null</c>, wenn die Eingabe gültig ist. Beim
    /// Setzen werden <see cref="HasErrors"/>/<see cref="NotHasErrors"/> gemeldet und
    /// <see cref="ErrorsChanged"/> ausgelöst.
    /// </summary>
    public string TextError {
        get => _textError;
        set {
            if (SetProperty(ref _textError, value)) {
                // ReSharper disable ExplicitCallerInfoArgument
                NotifyPropertyChanged(nameof(HasErrors));
                NotifyPropertyChanged(nameof(NotHasErrors));
                // ReSharper restore ExplicitCallerInfoArgument
                ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(nameof(Text)));
            } }
    }
        
    /// <summary>
    /// Liefert die Validierungsfehler zur angegebenen Eigenschaft (aktuell nur der Eingabetext).
    /// </summary>
    /// <param name="propertyName">Name der abgefragten Eigenschaft.</param>
    /// <returns>Die Fehlermeldung(en) oder eine leere Folge, wenn kein Fehler vorliegt.</returns>
    public IEnumerable GetErrors(string propertyName) {
        if (TextError == null) {
            yield break;
        }
        yield return TextError;
    }

    /// <summary>Gegenteil von <see cref="HasErrors"/> — <c>true</c>, wenn die Eingabe gültig ist.</summary>
    public bool                                           NotHasErrors => !HasErrors;
    /// <summary>Gibt an, ob aktuell ein Validierungsfehler vorliegt.</summary>
    public bool                                           HasErrors    => !String.IsNullOrEmpty(TextError);
    /// <summary>Wird ausgelöst, wenn sich die Validierungsfehler geändert haben.</summary>
    public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

}