namespace Pharmatechnik.Nav.Language.Extension.Diagnostics; 

/// <summary>
/// WPF-Steuerelement (schmaler <see cref="System.Windows.Controls.Border"/>) für einen Diagnose-Streifen
/// im vertikalen Scrollbalken. Die im Editor tatsächlich gezeichnete Streifen-Randleiste ist
/// <see cref="DiagnosticStripeMargin"/>, die selbst von <see cref="System.Windows.Controls.Border"/> erbt
/// und ihre Marken direkt rendert; dieses XAML-Steuerelement wird derzeit nicht aus Code referenziert.
/// </summary>
public partial class DiagnosticStripeControl {
    /// <summary>Initialisiert das Steuerelement.</summary>
    public DiagnosticStripeControl() {
        InitializeComponent();
    }
}