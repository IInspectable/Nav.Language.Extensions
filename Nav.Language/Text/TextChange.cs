#region Using Directives

using System;

#endregion

namespace Pharmatechnik.Nav.Language.Text;

/// <summary>
/// Eine einzelne Text-Änderung: der von ihr betroffene <see cref="TextExtent"/> wird durch
/// <see cref="ReplacementText"/> ersetzt — das Roslyn-Analogon zu
/// <c>Microsoft.CodeAnalysis.Text.TextChange</c>. Der Extent und der Ersatztext kodieren zusammen alle
/// drei elementaren Operationen: ein leerer Extent (Länge 0) bei nicht-leerem Text ist ein <b>Einfügen</b>
/// (<see cref="NewInsert"/>), ein nicht-leerer Extent bei leerem Text ein <b>Löschen</b>
/// (<see cref="NewRemove(TextExtent)"/>), beide nicht-leer ein <b>Ersetzen</b>
/// (<see cref="NewReplace(TextExtent, string)"/>). Angewandt werden mehrere Changes gebündelt über
/// <see cref="TextChangeWriter.ApplyTextChanges"/> (etwa vom Formatter oder von Rename/CodeFixes erzeugt).
/// </summary>
public readonly struct TextChange: IEquatable<TextChange> {

    private readonly string? _replacementText;

    TextChange(TextExtent extent, string replacementText) {
        Extent           = extent;
        _replacementText = replacementText ?? throw new ArgumentNullException(nameof(replacementText));
    }

    /// <summary>
    /// Erzeugt ein Einfügen: der <paramref name="text"/> wird an <paramref name="position"/> eingefügt
    /// (leerer <see cref="Extent"/> an dieser Position), ohne vorhandenen Text zu ersetzen.
    /// </summary>
    /// <param name="position">Die Einfüge-Position im Quelltext.</param>
    /// <param name="text">Der einzufügende Text.</param>
    public static TextChange NewInsert(int position, string text) {
        return new TextChange(TextExtent.FromBounds(position, position), text);
    }

    /// <summary>
    /// Erzeugt ein Löschen des Bereichs <c>[start, start+length)</c> (leerer <see cref="ReplacementText"/>).
    /// </summary>
    /// <param name="start">Die Startposition des zu löschenden Bereichs.</param>
    /// <param name="length">Die Länge des zu löschenden Bereichs in Zeichen.</param>
    public static TextChange NewRemove(int start, int length) {
        return NewRemove(new TextExtent(start, length));
    }

    /// <summary>
    /// Erzeugt ein Löschen des angegebenen <paramref name="extent"/> (leerer <see cref="ReplacementText"/>).
    /// </summary>
    /// <param name="extent">Der zu löschende Bereich.</param>
    public static TextChange NewRemove(TextExtent extent) {
        return new TextChange(extent, String.Empty);
    }

    /// <summary>
    /// Erzeugt ein Ersetzen des Bereichs <c>[start, start+length)</c> durch <paramref name="text"/>.
    /// </summary>
    /// <param name="start">Die Startposition des zu ersetzenden Bereichs.</param>
    /// <param name="length">Die Länge des zu ersetzenden Bereichs in Zeichen.</param>
    /// <param name="text">Der Ersatztext.</param>
    public static TextChange NewReplace(int start, int length, string text) {
        return NewReplace(new TextExtent(start, length), text);
    }

    /// <summary>
    /// Erzeugt ein Ersetzen des angegebenen <paramref name="extent"/> durch <paramref name="text"/>.
    /// </summary>
    /// <param name="extent">Der zu ersetzende Bereich.</param>
    /// <param name="text">Der Ersatztext.</param>
    public static TextChange NewReplace(TextExtent extent, string text) {
        return new TextChange(extent, text);
    }

    /// <summary>
    /// Die leere Änderung (Default-Wert): leerer <see cref="Extent"/> und leerer <see cref="ReplacementText"/>,
    /// <see cref="IsEmpty"/> ist <c>true</c>.
    /// </summary>
    public static readonly TextChange Empty = new();

    /// <summary>
    /// Der Quelltext-Bereich, den diese Änderung ersetzt.
    /// </summary>
    public TextExtent Extent { get; }

    /// <summary>
    /// Der Text, der an die Stelle des <see cref="Extent"/> tritt — nie <c>null</c> (ein fehlender Wert
    /// wird auf <see cref="String.Empty"/> normalisiert, was einem Löschen entspricht).
    /// </summary>
    public string ReplacementText => _replacementText ?? String.Empty;

    /// <summary>
    /// Ob diese Änderung nichts bewirkt: leerer <see cref="Extent"/> und leerer <see cref="ReplacementText"/>.
    /// </summary>
    public bool IsEmpty => Extent.IsEmpty && ReplacementText == String.Empty;

    /// <summary>
    /// Liefert eine kompakte Kurzform aus <see cref="Extent"/> und <see cref="ReplacementText"/> für
    /// Debug-Ausgaben.
    /// </summary>
    public override string ToString() {
        return $"{nameof(TextChange)}: {{ {Extent}, \"{ReplacementText}\" }}";
    }

    #region Equality members

    /// <summary>
    /// Zwei Änderungen sind gleich, wenn <see cref="Extent"/> und <see cref="ReplacementText"/>
    /// übereinstimmen.
    /// </summary>
    /// <param name="other">Die zu vergleichende Änderung.</param>
    public bool Equals(TextChange other) {
        return Extent.Equals(other.Extent) && string.Equals(ReplacementText, other.ReplacementText);
    }

    /// <summary>
    /// Ermittelt, ob dieses Objekt einer anderen <see cref="TextChange"/> entspricht.
    /// </summary>
    /// <param name="obj">Das zu vergleichende Objekt.</param>
    public override bool Equals(object? obj) {
        if (ReferenceEquals(null, obj)) return false;

        return obj is TextChange change && Equals(change);
    }

    /// <summary>
    /// Liefert eine Hashfunktion für <see cref="TextChange"/> aus <see cref="Extent"/> und
    /// <see cref="ReplacementText"/>.
    /// </summary>
    public override int GetHashCode() {
        unchecked {
            return (Extent.GetHashCode() * 397) ^ ReplacementText.GetHashCode();
        }
    }

    /// <summary>
    /// Ermittelt, ob zwei <see cref="TextChange"/> gleich sind.
    /// </summary>
    public static bool operator ==(TextChange left, TextChange right) {
        return left.Equals(right);
    }

    /// <summary>
    /// Ermittelt, ob zwei <see cref="TextChange"/> verschieden sind.
    /// </summary>
    public static bool operator !=(TextChange left, TextChange right) {
        return !left.Equals(right);
    }

    #endregion

}