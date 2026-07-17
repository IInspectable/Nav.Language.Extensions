using System;

namespace Pharmatechnik.Nav.Language.Text;

/// <summary>
/// Ein unveränderlicher Quelltext-Ausschnitt als Wertetyp — die konkrete Implementierung von
/// <see cref="IExtent"/> und das Nav-Pendant zu Roslyns <c>Microsoft.CodeAnalysis.Text.TextSpan</c>.
/// Intern über <see cref="Start"/> und <see cref="Length"/> gehalten; <see cref="End"/> ergibt sich
/// als <see cref="Start"/> + <see cref="Length"/> (halboffenes Intervall, <see cref="End"/> exklusiv).
/// Ein <see cref="Start"/> von <c>-1</c> steht für einen fehlenden Ausschnitt (<see cref="Missing"/>).
/// </summary>
[Serializable]
public readonly struct TextExtent: IExtent, IEquatable<TextExtent> {

    /// <summary>
    /// Erzeugt einen Ausschnitt ab <paramref name="start"/> mit der Länge <paramref name="length"/>.
    /// </summary>
    /// <param name="start">Der nullbasierte Startindex (inklusiv); <c>-1</c> für einen fehlenden Ausschnitt.</param>
    /// <param name="length">Die Länge in Zeichen; muss ≥ 0 sein.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> ist negativ,
    /// <paramref name="start"/> ist kleiner als <c>-1</c>, oder es wird ein fehlender Ausschnitt
    /// (<paramref name="start"/> = <c>-1</c>) mit einer Länge größer 0 verlangt.</exception>
    public TextExtent(int start, int length) {

        if (length < 0) {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        if (start < -1) {
            throw new ArgumentOutOfRangeException(nameof(start));
        }

        if (start == -1 && length > 0) {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        Start  = start;
        Length = length;
    }

    /// <summary>Der leere Ausschnitt an Position 0 (<see cref="Start"/> = 0, <see cref="Length"/> = 0).</summary>
    public static readonly TextExtent Empty   = new(start: 0,  length: 0);
    /// <summary>
    /// Der fehlende Ausschnitt (<see cref="Start"/> = <c>-1</c>, <see cref="Length"/> = 0) — kennzeichnet
    /// eine nicht vorhandene/unbekannte Position; siehe <see cref="IsMissing"/>.
    /// </summary>
    public static readonly TextExtent Missing = new(start: -1, length: 0);

    /// <summary>
    /// Erzeugt einen Ausschnitt aus einem inklusiven <paramref name="start"/> und einem exklusiven
    /// <paramref name="end"/>; die Länge ergibt sich als <paramref name="end"/> − <paramref name="start"/>.
    /// </summary>
    /// <param name="start">Der nullbasierte Startindex (inklusiv).</param>
    /// <param name="end">Der nullbasierte Endindex (exklusiv).</param>
    /// <returns>Der Ausschnitt von <paramref name="start"/> bis ausschließlich <paramref name="end"/>.</returns>
    public static TextExtent FromBounds(int start, int end) {
        return new TextExtent(start: start, length: end - start);
    }

    /// <summary>Ob dieser Ausschnitt fehlt (<see cref="Start"/> &lt; 0); siehe <see cref="Missing"/>.</summary>
    public bool IsMissing        => Start  < 0;
    /// <summary>Ob dieser Ausschnitt leer ist (<see cref="Length"/> = 0).</summary>
    public bool IsEmpty          => Length == 0;
    /// <summary>Ob dieser Ausschnitt leer (<see cref="IsEmpty"/>) oder fehlend (<see cref="IsMissing"/>) ist.</summary>
    public bool IsEmptyOrMissing => IsEmpty || IsMissing;

    /// <inheritdoc/>
    public int Start { get; }

    /// <summary>Die Länge des Ausschnitts in Zeichen; stets ≥ 0.</summary>
    public int Length { get; }

    /// <inheritdoc/>
    public int End => Start + Length;

    /// <summary>
    /// Ob <paramref name="other"/> vollständig in diesem Ausschnitt enthalten ist
    /// (<paramref name="other"/>.<see cref="Start"/> ≥ <see cref="Start"/> und
    /// <paramref name="other"/>.<see cref="End"/> ≤ <see cref="End"/>).
    /// </summary>
    /// <param name="other">Der zu prüfende Ausschnitt.</param>
    public bool Contains(TextExtent other) {
        return other.Start >= Start && other.End <= End;
    }

    /// <summary>
    /// Ob sich <paramref name="extent"/> mit diesem Ausschnitt überschneidet. Zwei Ausschnitte
    /// überschneiden sich, wenn sie gemeinsame Positionen haben oder das Ende des einen mit dem
    /// Anfang des anderen zusammenfällt (Berührung an der Grenze zählt als Überschneidung).
    /// </summary>
    /// <param name="extent">Der zu prüfende Ausschnitt.</param>
    public bool IntersectsWith(TextExtent extent) {
        return extent.Start <= End && extent.End >= Start;
    }

    /// <summary>
    /// Liefert die Schnittmenge mit <paramref name="span"/>, oder <c>null</c>, wenn sich die
    /// Ausschnitte nicht schneiden.
    /// </summary>
    /// <param name="span">Der zu schneidende Ausschnitt.</param>
    /// <returns>Der überschneidende Ausschnitt, oder <c>null</c>, wenn keine Überschneidung besteht.</returns>
    public TextExtent? Intersection(TextExtent span) {
        int intersectStart = Math.Max(Start, span.Start);
        int intersectEnd   = Math.Min(End, span.End);

        return intersectStart <= intersectEnd ? FromBounds(intersectStart, intersectEnd) : null;
    }

    /// <summary>
    /// Liefert eine kompakte Darstellung der Form <c>[Start-End]</c>, bzw. <c>&lt;missing&gt;</c> für
    /// einen fehlenden Ausschnitt (<see cref="IsMissing"/>).
    /// </summary>
    public override string ToString() {
        if (IsMissing) {
            return "<missing>";
        }

        return $"[{Start}-{End}]";
    }

    /// <summary>Ob zwei <see cref="TextExtent"/> gleich sind.</summary>
    public static bool operator ==(TextExtent left, TextExtent right) {
        return left.Equals(right);
    }

    /// <summary>Ob zwei <see cref="TextExtent"/> verschieden sind.</summary>
    public static bool operator !=(TextExtent left, TextExtent right) {
        return !left.Equals(right);
    }

    /// <summary>Ob dieser Ausschnitt gleich <paramref name="other"/> ist.</summary>
    /// <param name="other">Der zu vergleichende Ausschnitt.</param>
    public bool Equals(TextExtent other) {
        return other.Start == Start && other.End == End;
    }

    /// <summary>Ob <paramref name="obj"/> ein gleicher <see cref="TextExtent"/> ist.</summary>
    /// <param name="obj">Das zu vergleichende Objekt.</param>
    public override bool Equals(object? obj) {
        return obj is TextExtent extent && Equals(extent);
    }

    /// <summary>Liefert einen Hashcode für diesen <see cref="TextExtent"/>.</summary>
    public override int GetHashCode() {
        return Start ^ End;
    }

}