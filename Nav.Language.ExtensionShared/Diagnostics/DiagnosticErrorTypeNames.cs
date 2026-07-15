#region Using Directives

using Microsoft.VisualStudio.Text.Adornments;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Diagnostics; 

/// <summary>
/// Bildet die Nav-Schweregrade auf die vordefinierten VS-Fehlertyp-Namen
/// (<see cref="PredefinedErrorTypeNames"/>) ab, die Darstellung und Farbe der Editor-Schlangenlinien
/// steuern. Verwendet von <see cref="DiagnosticErrorTag"/> und <see cref="DiagnosticStripeMargin"/>.
/// </summary>
static class DiagnosticErrorTypeNames {

    /// <summary>Fehlertyp für Fehler (Schweregrad Error) — rote Schlangenlinie.</summary>
    public const string Error      = PredefinedErrorTypeNames.SyntaxError;
    /// <summary>Fehlertyp für Warnungen — grüne Schlangenlinie.</summary>
    public const string Warning    = PredefinedErrorTypeNames.Warning;
    /// <summary>Fehlertyp für Vorschläge (auch „Dead Code") — unsichtbare Schlangenlinie mit Tooltip.</summary>
    public const string Suggestion = PredefinedErrorTypeNames.Suggestion;
}