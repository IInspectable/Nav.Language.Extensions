namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Die fachliche Einordnung einer Diagnose. Jeder <see cref="DiagnosticDescriptor"/> trägt genau eine
/// Kategorie (<see cref="DiagnosticDescriptor.Category"/>); sie ordnet den Fehlercode grob der Stelle
/// in der Verarbeitungskette zu, an der die Diagnose entsteht, und erscheint als zweite Spalte in
/// <c>doc/Errors.md</c>.
/// </summary>
public enum DiagnosticCategory {
    /// <summary>
    /// Ein interner Fehler der Engine (kein Anwenderfehler im <c>.nav</c>-Quelltext), der auf einen
    /// Defekt oder eine unerwartete Situation im Werkzeug selbst hinweist.
    /// </summary>
    Internal,
    /// <summary>
    /// Ein Verstoß gegen die Grammatik der Nav-Sprache, der von Lexer bzw. Parser (<c>Syntax\</c>)
    /// gemeldet wird — etwa ein unerwartetes Zeichen oder ein fehlendes Token.
    /// </summary>
    Syntax,
    /// <summary>
    /// Ein Verstoß gegen die semantischen Regeln (Namensauflösung, Typ-/Referenz-Bindung, …), der vom
    /// <c>SemanticAnalyzer\</c> auf dem bereits geparsten Baum festgestellt wird.
    /// </summary>
    Semantic,
    /// <summary>
    /// Ein Hinweis auf toten Code — eine Deklaration oder Direktive, die vom generierten Code nicht
    /// benötigt wird und gefahrlos entfernt werden kann (Fehlercodes <c>Nav1xxx</c>).
    /// </summary>
    DeadCode
}