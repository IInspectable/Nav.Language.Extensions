namespace Pharmatechnik.Nav.Language.Formatting;

/// <summary>
/// Eine Layout-Regel des Formatters: eine reine, isoliert testbare Mini-Funktion über den vorberechneten
/// <see cref="GapContext"/>. Liefert <c>null</c> („nicht zuständig", nächste Regel fragen) oder genau ein
/// vollständiges <see cref="GapLayout"/> — die erste passende Regel gewinnt (<see cref="GapRules"/>),
/// es wird nie etwas kombiniert.
/// </summary>
/// <remarks>
/// Regeln lesen ausschließlich formatierungs-invariante Fakten aus dem Kontext (Token-Typen, Baumstruktur,
/// Newline-Anzahl) — nie das aktuelle Whitespace. Nur so ist Idempotenz eine lokale Eigenschaft jeder Regel
/// statt einer emergenten des Gesamtsystems.
/// </remarks>
interface IGapRule {

    /// <summary>Der Prioritäts-Tier dieser Regel (siehe <see cref="RulePriority"/>).</summary>
    RulePriority Tier { get; }

    /// <summary>Die Layout-Entscheidung für die Lücke, oder <c>null</c>, wenn diese Regel nicht zuständig ist.</summary>
    GapLayout? Apply(in GapContext ctx);

}
