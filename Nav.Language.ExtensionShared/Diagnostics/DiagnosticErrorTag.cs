#region Using Directives

using Microsoft.VisualStudio.Text.Tagging;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Diagnostics; 

/// <summary>
/// VS-Fehler-Tag (<see cref="IErrorTag"/>), das eine einzelne Nav-<see cref="Diagnostic"/> im Editor als
/// Schlangenlinie (Squiggle) mit Tooltip darstellt. Erzeugt vom <see cref="DiagnosticErrorTagger"/>; die
/// <see cref="DiagnosticSeverity"/> wird auf den passenden VS-Fehlertyp (<see cref="DiagnosticErrorTypeNames"/>)
/// abgebildet.
/// </summary>
public sealed class DiagnosticErrorTag : IErrorTag {
    readonly Diagnostic _diagnostic;

    /// <summary>Erzeugt ein Fehler-Tag für die angegebene <paramref name="diagnostic"/>.</summary>
    public DiagnosticErrorTag(Diagnostic diagnostic){
        _diagnostic = diagnostic;
    }
       
    /// <summary>
    /// VS-Fehlertyp, der Darstellung und Farbe der Schlangenlinie bestimmt. Bildet den
    /// <see cref="DiagnosticSeverity"/> auf <see cref="DiagnosticErrorTypeNames"/> ab; „Dead Code" unterhalb
    /// des Schweregrads Error wird bewusst als (unsichtbarer) <see cref="DiagnosticErrorTypeNames.Suggestion"/>
    /// geführt.
    /// </summary>
    public string ErrorType {
        get {
            // Sonderlocke "Dead Code"
            if(_diagnostic.Category == DiagnosticCategory.DeadCode && 
               _diagnostic.Severity != DiagnosticSeverity.Error) {
                // "Suggestion" führt zu einem unsichbaren squiggle,
                // für den aber dennoch ein Tooltip angezeigt wird.
                // "Dead Code" wird durch leichtes Ausblenden extra visualisiert.
                return DiagnosticErrorTypeNames.Suggestion;
            }

            switch(_diagnostic.Severity) {
                case DiagnosticSeverity.Suggestion:
                    return DiagnosticErrorTypeNames.Suggestion;
                case DiagnosticSeverity.Warning:
                    return DiagnosticErrorTypeNames.Warning;
                case DiagnosticSeverity.Error:
                default:
                    return DiagnosticErrorTypeNames.Error;
            }                
        }
    }

    /// <summary>Tooltip-Inhalt der Schlangenlinie — die Meldung der <see cref="Diagnostic"/>.</summary>
    public object ToolTipContent {
        get { return _diagnostic.Message; }
    }

    /// <summary>Die dargestellte <see cref="Diagnostic"/>.</summary>
    public Diagnostic Diagnostic {
        get { return _diagnostic; }
    }
}