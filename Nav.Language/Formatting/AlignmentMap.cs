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
    public static readonly AlignmentMap Empty = new(new Dictionary<int, int>(), new Dictionary<int, int>());

    readonly Dictionary<int, int> _spacesByGapStart;
    readonly Dictionary<int, int> _trailingCommentSpacesByGapStart;

    /// <summary>Die vom Vorpass (<see cref="AlignmentMapBuilder"/>) berechnete Tabelle.</summary>
    /// <param name="spacesByGapStart">Token-Ausrichtung: Lücke → aufgelöste Space-Zahl.</param>
    /// <param name="trailingCommentSpacesByGapStart">Trailing-<c>//</c>-Kommentar-Ausrichtung: Lücke hinter
    /// dem letzten Token einer Anweisung → Space-Zahl bis zur gemeinsamen Kommentar-Spalte.</param>
    public AlignmentMap(Dictionary<int, int> spacesByGapStart, Dictionary<int, int> trailingCommentSpacesByGapStart) {
        _spacesByGapStart                = spacesByGapStart;
        _trailingCommentSpacesByGapStart = trailingCommentSpacesByGapStart;
    }

    /// <summary>
    /// Die aufgelöste Space-Zahl für die Lücke mit Startposition <paramref name="gapStart"/>, oder
    /// <c>false</c>, wenn die Lücke an keiner Ausrichtung teilnimmt.
    /// </summary>
    public bool TryGetSpaces(int gapStart, out int spaces) {
        return _spacesByGapStart.TryGetValue(gapStart, out spaces);
    }

    /// <summary>
    /// Die Space-Zahl vor einem Trailing-<c>//</c>-Kommentar (Lücke hinter dem letzten Token einer
    /// Anweisung, Startposition <paramref name="gapStart"/>), sodass der Kommentar auf der gemeinsamen
    /// Spalte seines Blocks sitzt — oder <c>false</c>, wenn der Kommentar an keiner Ausrichtung teilnimmt
    /// (dann rendert der <see cref="GapRenderer"/> das übliche eine Leerzeichen davor).
    /// </summary>
    public bool TryGetTrailingCommentSpaces(int gapStart, out int spaces) {
        return _trailingCommentSpacesByGapStart.TryGetValue(gapStart, out spaces);
    }

}
