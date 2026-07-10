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

    /// <summary>Spalte 2 des Node-Deklarations-Rasters: Lücke <c>keyword → node</c>-Identifier.</summary>
    Node,

    /// <summary>Spalte 3 des Node-Deklarations-Rasters: Lücke <c>node</c>-Identifier <c>→ rest</c>.</summary>
    DeclRest,

    /// <summary>Task-Kopf-Block-Stapel: Folgeblöcke linksbündig unter dem <c>[</c> des ersten Blocks.</summary>
    TaskHeadBlock,

    /// <summary>Mehrzeiliges <c>[params]</c> im Task-Kopf: Folgeparameter unter dem ersten Parameter.</summary>
    ParamsList,

}