namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Der Schweregrad einer Diagnose — Roslyn-Analogon <c>Microsoft.CodeAnalysis.DiagnosticSeverity</c>.
/// Jeder <see cref="DiagnosticDescriptor"/> trägt einen Standard-Schweregrad
/// (<see cref="DiagnosticDescriptor.DefaultSeverity"/>). Die Reihenfolge der Werte ist aufsteigend nach
/// Ernst der Meldung; <see cref="DiagnosticSeverityExtension.GetWorst(DiagnosticSeverity, DiagnosticSeverity)"/>
/// wertet den zugrunde liegenden Zahlenwert aus, um den schwersten von mehreren Schweregraden zu bestimmen.
/// </summary>
public enum DiagnosticSeverity {

    /// <summary>
    /// Ein unverbindlicher Verbesserungsvorschlag — die schwächste Stufe; verhindert die Codegenerierung
    /// nicht.
    /// </summary>
    Suggestion,
    /// <summary>
    /// Eine Warnung: ein Problem, das Beachtung verdient, die Verarbeitung aber nicht abbricht.
    /// </summary>
    Warning,
    /// <summary>
    /// Ein Fehler — die schwerste Stufe; die Codegenerierung ist damit nicht mehr zulässig
    /// (siehe <see cref="DiagnosticExtensions.HasErrors"/>).
    /// </summary>
    Error

}