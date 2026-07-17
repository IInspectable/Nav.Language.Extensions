#region Using Directives

using System.Windows;
using Microsoft.VisualStudio.PlatformUI;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Common; 

/// <summary>
/// Code-Behind des modalen Eingabedialogs (<see cref="DialogWindow"/>), der ein
/// <see cref="InputDialogViewModel"/> an das XAML-Markup bindet.
/// </summary>
partial class InputDialog : DialogWindow {

    readonly InputDialogViewModel _viewModel;

    /// <summary>
    /// Initialisiert den Dialog mit dem angegebenen ViewModel und setzt es als
    /// <see cref="System.Windows.FrameworkElement.DataContext"/>.
    /// </summary>
    /// <param name="viewModel">Das an den Dialog gebundene ViewModel.</param>
    public InputDialog(InputDialogViewModel viewModel) {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext           =  _viewModel;
        InputText.TextChanged += OnTextBoxTextChanged;
    }

    /// <summary>
    /// Markiert beim ersten Textwechsel den gesamten Eingabetext und meldet sich anschließend
    /// wieder vom Ereignis ab.
    /// </summary>
    void OnTextBoxTextChanged(object sender, RoutedEventArgs e) {
        InputText.SelectAll();
        InputText.TextChanged -= OnTextBoxTextChanged;
    }

    /// <summary>
    /// Schließt den Dialog mit „OK", sofern das ViewModel keine Validierungsfehler meldet.
    /// </summary>
    void OnOkClick(object sender, RoutedEventArgs e) {
        if (_viewModel.HasErrors) {
            // TODO Button Deaktivieren, wenn Fehler vorhanden
            return;
        }
        DialogResult = true;
    }

    /// <summary>Bricht den Dialog ab.</summary>
    void OnCancelClick(object sender, RoutedEventArgs e) {
        DialogResult = false;
    }
}