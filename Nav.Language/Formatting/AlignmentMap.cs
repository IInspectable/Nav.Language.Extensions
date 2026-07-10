using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.Formatting;

/// <summary>
/// Die vorberechnete Ausrichtungs-Tabelle: je Lücke (identifiziert über ihre Startposition = Ende des
/// vorangehenden Tokens) die aufgelöste horizontale Space-Zahl. Die Ausrichtung ist die einzige
/// nicht-lokale Zutat des Formatters — der Vorpass rechnet Gruppen, kanonische Breiten und Zielspalte
/// block-weit aus und legt hier nur das Ergebnis ab; die Ausrichtungs-Regeln und der Renderer schlagen
/// ausschließlich nach und bleiben dadurch pur.
/// </summary>
/// <remarks>
/// Abgelegt wird die bereits aufgelöste Space-Zahl statt der Zielspalte, weil nur der Vorpass die
/// kanonischen Vor-Spalten-Breiten kennt: für <see cref="GapLayout.AlignedColumn"/> ist das
/// <c>targetCol − kanonische Breite</c> (Padding, ≥ 1), für <see cref="GapLayout.NewLineAlignedColumn"/>
/// die absolute Spalte nach dem Umbruch. Ausrichtungs-Padding ist immer Leerzeichen, nie Tabs.
/// </remarks>
sealed class AlignmentMap {

    /// <summary>Die leere Tabelle — kein Eintrag, jede Nachfrage fällt auf das Regel-/Renderer-Fallback.</summary>
    public static readonly AlignmentMap Empty = new(new Dictionary<int, int>());

    readonly Dictionary<int, int> _spacesByGapStart;

    /// <summary>Die vom Vorpass (<see cref="AlignmentMapBuilder"/>) berechnete Tabelle.</summary>
    public AlignmentMap(Dictionary<int, int> spacesByGapStart) {
        _spacesByGapStart = spacesByGapStart;
    }

    /// <summary>
    /// Die aufgelöste Space-Zahl für die Lücke mit Startposition <paramref name="gapStart"/>, oder
    /// <c>false</c>, wenn die Lücke an keiner Ausrichtung teilnimmt.
    /// </summary>
    public bool TryGetSpaces(int gapStart, out int spaces) {
        return _spacesByGapStart.TryGetValue(gapStart, out spaces);
    }

}
