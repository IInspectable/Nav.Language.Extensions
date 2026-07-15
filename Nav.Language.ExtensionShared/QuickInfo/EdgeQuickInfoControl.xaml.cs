#region Using Directives

using System.Windows.Controls;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.QuickInfo; 

/// <summary>
/// WPF-Code-Behind des QuickInfo-Controls, das für einen Choice-Knoten die erreichbaren Ziele (Kanten/Calls)
/// auflistet. Der Inhalt wird per <c>DataContext</c> aus einem Edge-View-Model gebunden (siehe
/// <see cref="QuickinfoBuilderService"/>); das Layout liegt im zugehörigen XAML.
/// </summary>
public partial class EdgeQuickInfoControl : StackPanel {
    /// <summary>Initialisiert das Control und lädt das zugehörige XAML-Layout.</summary>
    public EdgeQuickInfoControl() {
        InitializeComponent();
    }
}