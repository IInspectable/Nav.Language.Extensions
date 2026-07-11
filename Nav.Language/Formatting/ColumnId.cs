namespace Pharmatechnik.Nav.Language.Formatting;

/// <summary>
/// Benennt die Ausrichtungsspalten des Formatters. Jede Spalte ist eine Entscheidung auf genau einem
/// Lückentyp; ihre Zielposition wird block-weit vorberechnet (<see cref="AlignmentMap"/>) und von den
/// Ausrichtungs-Layouts (<see cref="GapLayout.AlignedColumn"/>/<see cref="GapLayout.NewLineAlignedColumn"/>)
/// nur nachgeschlagen.
/// </summary>
enum ColumnId {

    /// <summary>Pfeil-Spalte einer Transitions-Gruppe: Lücke zwischen Quell-Teil und Edge-Keyword.</summary>
    Arrow,

    /// <summary>
    /// Continuation-Spalte einer Transitions-Gruppe: Lücke vor der führenden Fortsetzungs-Kante
    /// (<c>--^</c>/<c>o-^</c> der <see cref="ContinuationTransitionSyntax"/>). Richtet aufeinanderfolgende
    /// Continuations unter dem längsten Ziel-Teil aus; steht in Quellreihenfolge zwischen Ziel-Teil und
    /// Trigger, misst kanonisch ab Zeilenanfang und baut dabei auf die bereits aufgelöste Pfeil-Spalte auf.
    /// Die Gruppenbildung ist — wie bei den Trailing-Kommentaren — bereits durch <b>eine einzelne</b>
    /// Leerzeile bzw. Kommentarzeile unterbrochen.
    /// </summary>
    Continuation,

    /// <summary>
    /// Trigger-Spalte einer Transitions-Gruppe: Lücke vor dem führenden <c>on</c>/<c>spontaneous</c> der
    /// <see cref="TriggerSyntax"/>. Richtet aufeinanderfolgende Trigger unter dem längsten Ziel-Teil aus;
    /// misst kanonisch ab Zeilenanfang und baut dabei auf die bereits aufgelösten Pfeil- und
    /// Continuation-Spalten auf. Die Gruppenbildung ist — wie bei den Trailing-Kommentaren — bereits durch
    /// <b>eine einzelne</b> Leerzeile bzw. Kommentarzeile unterbrochen.
    /// </summary>
    Trigger,

    /// <summary>
    /// Condition-Spalte einer Transitions-Gruppe: Lücke vor dem <c>if</c>/<c>else</c>/<c>else if</c> der
    /// <see cref="ConditionClauseSyntax"/> (das führende Keyword der Klausel). Richtet aufeinanderfolgende
    /// Bedingungen unter dem längsten Ziel-Teil aus; misst kanonisch ab Zeilenanfang und baut dabei auf
    /// die bereits aufgelösten Pfeil-, Continuation- und Trigger-Spalten auf. Die Gruppenbildung ist — wie bei den
    /// Trailing-Kommentaren — bereits durch <b>eine einzelne</b> Leerzeile bzw. Kommentarzeile unterbrochen.
    /// </summary>
    Condition,

    /// <summary>Spalte 2 des Node-Deklarations-Rasters: Lücke <c>keyword → node</c>-Identifier.</summary>
    Node,

    /// <summary>Spalte 3 des Node-Deklarations-Rasters: Lücke <c>node</c>-Identifier <c>→ rest</c>.</summary>
    DeclRest,

    /// <summary>
    /// Eigene Spalte für <c>[params]</c>-Blöcke an Node-Deklarationen (<c>init</c>/<c>choice</c>): Lücke
    /// <c>node</c>-Identifier <c>→ [</c>. Bewusst getrennt von <see cref="DeclRest"/> (Alias), damit ein
    /// langer Alias/Node den schwergewichtigen <c>[params]</c>-Block nicht unnötig nach rechts schiebt;
    /// tight ausgerichtet (kein Tab-Stopp), nur bei ≥ 2 params-Teilnehmern je Gruppe.
    /// </summary>
    NodeParams,

    /// <summary>Task-Kopf-Block-Stapel: Folgeblöcke linksbündig unter dem <c>[</c> des ersten Blocks.</summary>
    TaskHeadBlock,

    /// <summary>Mehrzeiliges <c>[params]</c> im Task-Kopf: Folgeparameter unter dem ersten Parameter.</summary>
    ParamsList,

    // Anmerkung: Die Trailing-//-Kommentar-Spalte hat bewusst keinen ColumnId — sie läuft nicht über ein
    // GapLayout, sondern wird direkt vom GapRenderer über AlignmentMap.TryGetTrailingCommentSpaces gesetzt.

}