using System;

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Ein monotoner Versions-Stempel zur Erkennung, ob sich etwas geändert hat (z.B. zum Invalidieren von
/// Caches). Er besteht aus einem UTC-Zeitstempel und einem Zähler (<c>_increment</c>), der zwei im selben
/// Augenblick erzeugte Stempel unterscheidbar macht. Stempel sind vergleichbar (siehe die Operatoren und
/// <see cref="GetNewer(VersionStamp)"/>): „neuer" heißt späterer Zeitstempel bzw. bei gleichem Zeitpunkt
/// höherer Zähler. Roslyn-Analogon <c>Microsoft.CodeAnalysis.VersionStamp</c>.
/// </summary>
[Serializable]
public struct VersionStamp: IEquatable<VersionStamp> {

    readonly DateTime _utcTimeStamp;
    readonly int      _increment;

    /// <summary>
    /// Erzeugt einen Stempel zum angegebenen UTC-Zeitpunkt (mit Zähler 0).
    /// </summary>
    /// <param name="utcTimeStamp">Der UTC-Zeitstempel.</param>
    public VersionStamp(DateTime utcTimeStamp): this(utcTimeStamp, 0) {

    }

    VersionStamp(DateTime utcTimeStamp, int increment) {
        _utcTimeStamp = utcTimeStamp;
        _increment    = increment;

    }

    /// <summary>Erzeugt einen Stempel zum aktuellen Zeitpunkt (<see cref="DateTime.UtcNow"/>).</summary>
    public static VersionStamp Create() {
        return new VersionStamp(DateTime.UtcNow);
    }

    /// <summary>Erzeugt einen Stempel zum angegebenen UTC-Zeitpunkt.</summary>
    /// <param name="utcTimeStamp">Der UTC-Zeitstempel.</param>
    public static VersionStamp Create(DateTime utcTimeStamp) {
        return new VersionStamp(utcTimeStamp);
    }

    /// <summary>
    /// Liefert einen Stempel, der garantiert neuer ist als dieser: zum aktuellen Zeitpunkt, bei
    /// gleichem Zeitpunkt mit um eins erhöhtem Zähler.
    /// </summary>
    public VersionStamp CreateNewer() {

        var utcTimeStamp = DateTime.UtcNow;
        var increment    = _utcTimeStamp == utcTimeStamp ? _increment + 1 : 0;
        return new VersionStamp(utcTimeStamp, increment);
    }

    /// <summary>Liefert den neueren dieses und des <paramref name="other"/>-Stempels.</summary>
    /// <param name="other">Der zu vergleichende Stempel.</param>
    public VersionStamp GetNewer(VersionStamp other) {

        return GetNewer(this, other);
    }

    /// <summary>
    /// Liefert den neueren zweier Stempel — den mit dem späteren Zeitstempel, bei Gleichstand den mit
    /// dem höheren Zähler (bei völliger Gleichheit <paramref name="b"/>).
    /// </summary>
    /// <param name="a">Der erste Stempel.</param>
    /// <param name="b">Der zweite Stempel.</param>
    public static VersionStamp GetNewer(VersionStamp a, VersionStamp b) {
        if (b._utcTimeStamp > a._utcTimeStamp) {
            return b;
        }

        if (b._utcTimeStamp == a._utcTimeStamp) {
            return b._increment > a._increment ? b : a;
        }

        return a;
    }

    /// <summary>
    /// Zwei Stempel sind gleich, wenn Zeitstempel und Zähler übereinstimmen.
    /// </summary>
    /// <param name="other">Der zu vergleichende Stempel.</param>
    public bool Equals(VersionStamp other) {
        return _utcTimeStamp.Equals(other._utcTimeStamp) &&
               _increment == other._increment;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) {
        if (ReferenceEquals(null, obj)) {
            return false;
        }

        return obj is VersionStamp stamp && Equals(stamp);
    }

    /// <summary>Liefert einen zur Gleichheit passenden Hashcode aus Zeitstempel und Zähler.</summary>
    public override int GetHashCode() {
        unchecked {
            return (_utcTimeStamp.GetHashCode() * 397) ^ _increment;
        }
    }

    /// <summary>Prüft zwei Stempel auf Gleichheit (siehe <see cref="Equals(VersionStamp)"/>).</summary>
    public static bool operator ==(VersionStamp left, VersionStamp right) {
        return left.Equals(right);
    }

    /// <summary>Prüft zwei Stempel auf Ungleichheit (siehe <see cref="Equals(VersionStamp)"/>).</summary>
    public static bool operator !=(VersionStamp left, VersionStamp right) {
        return !left.Equals(right);
    }

    /// <summary>Gibt an, ob <paramref name="left"/> älter ist als <paramref name="right"/>.</summary>
    public static bool operator <(VersionStamp left, VersionStamp right) {
        if (left == right) {
            return false;
        }

        return GetNewer(left, right) == right;

    }

    /// <summary>Gibt an, ob <paramref name="left"/> neuer ist als <paramref name="right"/>.</summary>
    public static bool operator >(VersionStamp left, VersionStamp right) {
        if (left == right) {
            return false;
        }

        return GetNewer(left, right) == left;
    }

    /// <summary>Gibt an, ob <paramref name="left"/> älter als oder gleich <paramref name="right"/> ist.</summary>
    public static bool operator <=(VersionStamp left, VersionStamp right) {
        if (left == right) {
            return true;
        }

        return left < right;
    }

    /// <summary>Gibt an, ob <paramref name="left"/> neuer als oder gleich <paramref name="right"/> ist.</summary>
    public static bool operator >=(VersionStamp left, VersionStamp right) {
        if (left == right) {
            return true;
        }

        return left > right;
    }

}