using System;

namespace Pharmatechnik.Nav.Language.Text;

/// <summary>
/// Ein zeilenbasierter Bereich zwischen zwei <see cref="LinePosition"/> — das Nav-Pendant zu Roslyns
/// <c>Microsoft.CodeAnalysis.Text.LinePositionSpan</c> und die zeilen-/spaltenbasierte Entsprechung
/// zu <see cref="TextExtent"/>. <see cref="Start"/> liegt nie hinter <see cref="End"/>. Aus einem
/// <see cref="TextExtent"/> wird ein solcher Bereich von <see cref="SourceText"/> gebildet (beide
/// Grenzen als nullbasierte <see cref="LinePosition"/>), etwa für die Zeileninformation einer
/// <see cref="Pharmatechnik.Nav.Language.Location"/>.
/// </summary>
[Serializable]
public readonly struct LineRange: IEquatable<LineRange> {

    /// <summary>Erzeugt einen Bereich von <paramref name="start"/> bis <paramref name="end"/>.</summary>
    /// <param name="start">Die Startposition; darf nicht hinter <paramref name="end"/> liegen.</param>
    /// <param name="end">Die Endposition.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="start"/> liegt hinter
    /// <paramref name="end"/>.</exception>
    public LineRange(LinePosition start, LinePosition end) {

        if (start > end) {
            throw new ArgumentOutOfRangeException(nameof(start));
        }

        Start = start;
        End   = end;
    }

    /// <summary>Die Startposition des Bereichs.</summary>
    public LinePosition Start { get; }
    /// <summary>Die Endposition des Bereichs; liegt nie vor <see cref="Start"/>.</summary>
    public LinePosition End   { get; }

    /// <summary>Ob zwei <see cref="LineRange"/> gleich sind.</summary>
    public static bool operator ==(LineRange left, LineRange right) {
        return left.Equals(right);
    }

    /// <summary>Ob zwei <see cref="LineRange"/> verschieden sind.</summary>
    public static bool operator !=(LineRange left, LineRange right) {
        return !left.Equals(right);
    }

    /// <summary>Ob dieser Bereich gleich <paramref name="other"/> ist.</summary>
    /// <param name="other">Der zu vergleichende Bereich.</param>
    public bool Equals(LineRange other) {
        return other.Start == Start && other.End == End;
    }

    /// <summary>Ob <paramref name="obj"/> ein gleicher <see cref="LineRange"/> ist.</summary>
    /// <param name="obj">Das zu vergleichende Objekt.</param>
    public override bool Equals(object? obj) {
        return obj is LineRange extent && Equals(extent);
    }

    /// <summary>Liefert einen Hashcode für diesen <see cref="LineRange"/>.</summary>
    public override int GetHashCode() {
        return Start.GetHashCode() ^ End.GetHashCode();
    }

}