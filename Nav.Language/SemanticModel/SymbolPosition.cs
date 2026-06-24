#region Using Directives

using System.Collections.Generic;
using System.Linq;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Caret-/Positions-Auflösung auf dem Symbol-Modell für Navigations-Features (GoTo, References,
/// Highlight). Bewusst NICHT der <c>Symbols[extent, includeOverlapping]</c>-Indexer: dessen „Punktsuche"
/// liefert bei Null-Längen-Extents immer das nächstgelegene vorangehende Symbol — auch wenn der Caret
/// gar nicht darauf steht. Hier zählt echtes Enthaltensein.
/// </summary>
static class SymbolPosition {

    /// <summary>
    /// Alle Symbole, deren Bereich <paramref name="position"/> (0-basierter Offset) enthält — vom
    /// spezifischsten (kürzester Bereich) zum allgemeinsten. Position am Bereichsrand zählt als enthalten.
    /// </summary>
    public static IEnumerable<ISymbol> SymbolsAt(CodeGenerationUnit unit, int position) {
        return unit.Symbols
                   .Where(s => s.Location != null && s.Start <= position && position <= s.End)
                   .OrderBy(s => s.End - s.Start);
    }

}
