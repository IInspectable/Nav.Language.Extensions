using System;

namespace Pharmatechnik.Nav.Language.Text;

/// <summary>
/// Eine nullbasierte Zeilen-/Spalten-Position in einem Quelltext — das Nav-Pendant zu Roslyns
/// <c>Microsoft.CodeAnalysis.Text.LinePosition</c>. Sowohl <see cref="Line"/> als auch
/// <see cref="Character"/> zählen ab 0 (erste Zeile bzw. erstes Zeichen = 0). Die Ordnung ist
/// lexikografisch: zuerst nach <see cref="Line"/>, bei Gleichstand nach <see cref="Character"/>.
/// </summary>
[Serializable]
public readonly struct LinePosition: IEquatable<LinePosition>, IComparable<LinePosition> {

    readonly int _line;
    readonly int _character;

    /// <summary>
    /// Erzeugt eine Position aus nullbasierter <paramref name="line"/> und <paramref name="character"/>.
    /// </summary>
    /// <param name="line">Die nullbasierte Zeilennummer; muss ≥ 0 sein.</param>
    /// <param name="character">Die nullbasierte Spalte innerhalb der Zeile; muss ≥ 0 sein.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="line"/> oder
    /// <paramref name="character"/> ist negativ.</exception>
    public LinePosition(int line, int character) {

        if (line < 0) {
            throw new ArgumentOutOfRangeException(nameof(line));
        }

        if (character < 0) {
            throw new ArgumentOutOfRangeException(nameof(character));
        }

        _line      = line;
        _character = character;
    }

    // TODO Missing hinzufügen
    /// <summary>Die Position am Dokumentanfang (<see cref="Line"/> = 0, <see cref="Character"/> = 0).</summary>
    public static readonly LinePosition Empty = new(0, 0);

    /// <summary>Die nullbasierte Zeilennummer; die erste Zeile einer Datei ist Zeile 0.</summary>
    public int Line => _line;

    /// <summary>Die nullbasierte Spalte innerhalb der Zeile.</summary>
    public int Character => _character;

    /// <summary>Ob zwei <see cref="LinePosition"/> gleich sind.</summary>
    public static bool operator ==(LinePosition left, LinePosition right) {
        return left.Equals(right);
    }

    /// <summary>Ob zwei <see cref="LinePosition"/> verschieden sind.</summary>
    public static bool operator !=(LinePosition left, LinePosition right) {
        return !left.Equals(right);
    }

    /// <summary>Ob diese Position gleich <paramref name="other"/> ist.</summary>
    /// <param name="other">Die zu vergleichende Position.</param>
    public bool Equals(LinePosition other) {
        return other.Line == Line && other.Character == Character;
    }

    /// <summary>Ob <paramref name="obj"/> eine gleiche <see cref="LinePosition"/> ist.</summary>
    /// <param name="obj">Das zu vergleichende Objekt.</param>
    public override bool Equals(object? obj) {
        return obj is LinePosition position && Equals(position);
    }

    /// <summary>Liefert einen Hashcode für diese <see cref="LinePosition"/>.</summary>
    public override int GetHashCode() {
        return Line ^ Character;
    }

    /// <summary>
    /// Liefert eine Darstellung der Form <c>Zeile,Spalte</c>. Zur besseren Lesbarkeit werden Zeile
    /// und Spalte dabei auf 1-basierte Nummern umgerechnet (wie in der Editor-Anzeige).
    /// </summary>
    /// <example>1,5</example>
    public override string ToString() {
        return $"{Line + 1},{Character + 1}";
    }

    /// <summary>
    /// Vergleicht diese Position mit <paramref name="other"/>: zuerst nach <see cref="Line"/>, bei
    /// Gleichstand nach <see cref="Character"/>.
    /// </summary>
    /// <param name="other">Die zu vergleichende Position.</param>
    /// <returns>Ein negativer Wert, wenn diese Position vor <paramref name="other"/> liegt; <c>0</c>
    /// bei Gleichheit; andernfalls ein positiver Wert.</returns>
    public int CompareTo(LinePosition other) {
        int result = _line.CompareTo(other._line);
        return result != 0 ? result : _character.CompareTo(other.Character);
    }

    /// <summary>Ob <paramref name="left"/> hinter <paramref name="right"/> liegt.</summary>
    public static bool operator >(LinePosition left, LinePosition right) {
        return left.CompareTo(right) > 0;
    }

    /// <summary>Ob <paramref name="left"/> hinter oder auf <paramref name="right"/> liegt.</summary>
    public static bool operator >=(LinePosition left, LinePosition right) {
        return left.CompareTo(right) >= 0;
    }

    /// <summary>Ob <paramref name="left"/> vor <paramref name="right"/> liegt.</summary>
    public static bool operator <(LinePosition left, LinePosition right) {
        return left.CompareTo(right) < 0;
    }

    /// <summary>Ob <paramref name="left"/> vor oder auf <paramref name="right"/> liegt.</summary>
    public static bool operator <=(LinePosition left, LinePosition right) {
        return left.CompareTo(right) <= 0;
    }

}
