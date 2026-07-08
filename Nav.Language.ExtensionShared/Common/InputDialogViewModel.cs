#region Using Directives

using System;
using System.Collections;
using System.ComponentModel;

using JetBrains.Annotations;

using Microsoft.VisualStudio.Imaging.Interop;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Common; 

sealed class InputDialogViewModel : AbstractNotifyPropertyChanged, INotifyDataErrorInfo {

    [CanBeNull]
    public Func<string, string> Validator { get; set; }

    private string _promptText;
    public string PromptText {
        get => _promptText;
        set => SetProperty(ref _promptText, value);
    }

    private string _title;
    public string Title {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    private string _text;
    public string Text {
        get => _text;
        set {
            if (SetProperty(ref _text, value)) {
                TextError = Validator?.Invoke(Text);
            }
        }
    }

    private ImageMoniker _iconMoniker;
    public ImageMoniker IconMoniker {
        get => _iconMoniker;
        set => SetProperty(ref _iconMoniker, value);
    }

    public bool ShouldDisplayImpactText {
        get { return !String.IsNullOrEmpty(Note); }
    }

    private string _note;
    public string Note {
        get => _note;
        set {
            if (SetProperty(ref _note, value)) {
                // ReSharper disable ExplicitCallerInfoArgument
                NotifyPropertyChanged(nameof(ShouldDisplayImpactText));
            } }
    }

    private ImageMoniker _noteIconMoniker;
    public ImageMoniker NoteIconMoniker {
        get => _noteIconMoniker;
        set => SetProperty(ref _noteIconMoniker, value);
    }

    private string _textError;
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
        
    public IEnumerable GetErrors(string propertyName) {
        if (TextError == null) {
            yield break;
        }
        yield return TextError;
    }

    public bool                                           NotHasErrors => !HasErrors;
    public bool                                           HasErrors    => !String.IsNullOrEmpty(TextError);
    public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

}