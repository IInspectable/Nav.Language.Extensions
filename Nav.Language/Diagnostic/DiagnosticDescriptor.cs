#region Using Directives

using System;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Unveränderliche Beschreibung einer Diagnose-Art — die „Vorlage", aus der über
/// <see cref="Diagnostic"/> die konkreten, an eine <see cref="Location"/> gebundenen Meldungen
/// entstehen. Ein Deskriptor bündelt die stabile Diagnose-Identität (<see cref="Id"/>), die
/// Meldungsvorlage (<see cref="MessageFormat"/>), die fachliche Einordnung (<see cref="Category"/>)
/// und den Standard-Schweregrad (<see cref="DefaultSeverity"/>). Die vordefinierten Deskriptoren
/// der Engine liegen in <see cref="DiagnosticDescriptors"/>.
/// </summary>
public sealed class DiagnosticDescriptor : IEquatable<DiagnosticDescriptor> {

    /// <summary>
    /// Erzeugt einen neuen Diagnose-Deskriptor.
    /// </summary>
    /// <param name="id">
    /// Die stabile Diagnose-Kennung (z.B. <c>Nav0001</c>, siehe <see cref="DiagnosticId"/>). Darf
    /// nicht <c>null</c>, leer oder reiner Leerraum sein.
    /// </param>
    /// <param name="messageFormat">
    /// Die Meldungsvorlage. Kann Platzhalter im Stil von <see cref="String.Format(string, object[])"/>
    /// (<c>{0}</c>, <c>{1}</c>, …) enthalten, die beim Erzeugen eines <see cref="Diagnostic"/> aus den
    /// Meldungs-Argumenten gefüllt werden.
    /// </param>
    /// <param name="category">Die fachliche Einordnung der Diagnose.</param>
    /// <param name="defaultSeverity">Der Standard-Schweregrad der Diagnose.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="id"/> ist <c>null</c>, leer oder reiner Leerraum.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="messageFormat"/> ist <c>null</c>.</exception>
    public DiagnosticDescriptor(string id, string messageFormat, DiagnosticCategory category, DiagnosticSeverity defaultSeverity) {

        if (String.IsNullOrWhiteSpace(id)) {
            throw new ArgumentException("Diagnostic id can't be null or whitespace", nameof(id));
        }

        Id              = id;
        MessageFormat   = messageFormat ?? throw new ArgumentNullException(nameof(messageFormat));
        Category        = category;
        DefaultSeverity = defaultSeverity;
    }

    /// <summary>
    /// Die stabile Diagnose-Kennung (z.B. <c>Nav0001</c>). Über sie wird eine Diagnose-Art dauerhaft
    /// identifiziert (etwa zum Unterdrücken oder zum Nachschlagen).
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Die Meldungsvorlage; kann Platzhalter im Stil von <see cref="String.Format(string, object[])"/>
    /// enthalten, die erst beim Erzeugen eines <see cref="Diagnostic"/> aufgelöst werden.
    /// </summary>
    public string MessageFormat { get; }

    /// <summary>
    /// Die fachliche Einordnung der Diagnose (z.B. Syntax, Semantik).
    /// </summary>
    public DiagnosticCategory Category { get; }

    /// <summary>
    /// Der Standard-Schweregrad der Diagnose.
    /// </summary>
    public DiagnosticSeverity DefaultSeverity { get; }

    /// <summary>
    /// Zwei Deskriptoren gelten als gleich, wenn <see cref="Id"/>, <see cref="MessageFormat"/>,
    /// <see cref="Category"/> und <see cref="DefaultSeverity"/> übereinstimmen.
    /// </summary>
    public bool Equals(DiagnosticDescriptor? other) {

        if (ReferenceEquals(null, other)) {
            return false;
        }

        if (ReferenceEquals(this, other)) {
            return true;
        }

        return String.Equals(Id,            other.Id)            &&
               String.Equals(MessageFormat, other.MessageFormat) &&
               String.Equals(Category,      other.Category)      &&
               DefaultSeverity == other.DefaultSeverity;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) {
        if (ReferenceEquals(null, obj)) {
            return false;
        }
        if (ReferenceEquals(this, obj)) {
            return true;
        }
        return obj is DiagnosticDescriptor descriptor && Equals(descriptor);
    }

    /// <summary>
    /// Liefert einen zur Gleichheit passenden Hashcode aus <see cref="Id"/>,
    /// <see cref="MessageFormat"/>, <see cref="Category"/> und <see cref="DefaultSeverity"/>.
    /// </summary>
    public override int GetHashCode() {
        unchecked {
            var hashCode = Id.GetHashCode();
            hashCode = (hashCode * 397) ^ MessageFormat.GetHashCode();
            hashCode = (hashCode * 397) ^ Category.GetHashCode();
            hashCode = (hashCode * 397) ^ (int)DefaultSeverity;
            return hashCode;
        }
    }

    /// <summary>Prüft zwei Deskriptoren auf Gleichheit (siehe <see cref="Equals(DiagnosticDescriptor)"/>).</summary>
    public static bool operator ==(DiagnosticDescriptor? left, DiagnosticDescriptor? right) {
        return Equals(left, right);
    }

    /// <summary>Prüft zwei Deskriptoren auf Ungleichheit (siehe <see cref="Equals(DiagnosticDescriptor)"/>).</summary>
    public static bool operator !=(DiagnosticDescriptor? left, DiagnosticDescriptor? right) {
        return !Equals(left, right);
    }

    /// <summary>
    /// Liefert eine kompakte Textdarstellung im Format
    /// <c>&lt;Category&gt; &lt;DefaultSeverity&gt; &lt;Id&gt; : &lt;MessageFormat&gt;</c> (v.a. für
    /// Diagnose-/Debug-Zwecke).
    /// </summary>
    public override string ToString() {
        return $"{Category} {DefaultSeverity} {Id} : {MessageFormat}";
    }
}