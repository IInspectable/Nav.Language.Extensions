namespace Pharmatechnik.Nav.Language.Extension.Diagnostics; 

/// <summary>
/// WPF-Steuerelement der <see cref="DiagnosticSummaryMargin"/>: zeigt ein Status-Icon
/// (<c>CrispImage</c>), dessen Moniker den schlimmsten aktuellen <see cref="DiagnosticSeverity"/>
/// widerspiegelt, samt Tooltip mit Fehler-/Warnungszählung.
/// </summary>
public partial class DiagnosticSummaryControl  {
    /// <summary>Initialisiert das Steuerelement.</summary>
    public DiagnosticSummaryControl() {
        InitializeComponent();
    }
}