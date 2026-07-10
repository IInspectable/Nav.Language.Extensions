namespace Pharmatechnik.Nav.Language.Formatting;

/// <summary>
/// Prioritäts-Tier einer <see cref="IGapRule"/> — „spezifisch schlägt generisch": Sicherheit/Verbatim
/// zuerst, dann harte Struktur, dann Token-Paar-Regeln, dann Ausrichtung, zuletzt der Catch-all.
/// Der Dispatcher (<see cref="GapRules"/>) ordnet nach Tier, dann nach Deklarationsreihenfolge;
/// innerhalb eines Tiers müssen die Prädikate disjunkt sein (im Debug-Lauf geprüft) —
/// Cross-Tier-Overlaps sind gewollt (der höhere Tier preemptiert).
/// </summary>
enum RulePriority {

    /// <summary>Unterdrückung/Verbatim — preemptiert bewusst jede Layout-Regel.</summary>
    Safety,

    /// <summary>Harte Struktur: Klammern, Member-/Statement-Umbrüche, Blockgrenzen.</summary>
    Structure,

    /// <summary>Spezifische Token-Paar-Regeln (tight Doppelpunkt, Interpunktion, Task-Kopf).</summary>
    TokenPair,

    /// <summary>Spaltenausrichtung (Pfeil-Spalte, Node-Raster).</summary>
    Alignment,

    /// <summary>Der Catch-all — garantiert Totalität (jede Lücke bekommt eine Entscheidung).</summary>
    Default,

}
