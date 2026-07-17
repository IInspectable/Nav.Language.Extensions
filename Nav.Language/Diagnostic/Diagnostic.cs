#region Using Directives

using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Eine einzelne, an einer <see cref="Location"/> verortete Diagnose-Instanz — die konkrete Meldung,
/// die aus einer <see cref="DiagnosticDescriptor"/>-Vorlage und den Meldungs-Argumenten entsteht.
/// Roslyn-Analogon <c>Microsoft.CodeAnalysis.Diagnostic</c>. Der <see cref="Descriptor"/> liefert
/// stabile Identität (<see cref="DiagnosticDescriptor.Id"/>), <see cref="Category"/> und
/// <see cref="Severity"/>; die <see cref="Message"/> ergibt sich, indem die Meldungs-Argumente in
/// <see cref="DiagnosticDescriptor.MessageFormat"/> eingesetzt werden. Eine Diagnose ist
/// unveränderlich; <see cref="WithLocation"/> liefert eine Kopie an anderer Position.
/// </summary>
[Serializable]
public sealed class Diagnostic: IEquatable<Diagnostic> {

    readonly object[] _messageArgs;

    /// <summary>
    /// Erzeugt eine Diagnose an <paramref name="location"/> ohne Zusatz-Positionen.
    /// </summary>
    /// <param name="location">Die Hauptposition der Diagnose (Datei und Textausschnitt).</param>
    /// <param name="descriptor">Die Diagnose-Vorlage (Id, Meldungsformat, Kategorie, Schweregrad).</param>
    /// <param name="messageArgs">
    /// Die Argumente, die die Platzhalter in <see cref="DiagnosticDescriptor.MessageFormat"/> füllen;
    /// darf <c>null</c> sein (wird wie eine leere Argumentliste behandelt).
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="location"/> oder <paramref name="descriptor"/> ist <c>null</c>.
    /// </exception>
    public Diagnostic(Location location, DiagnosticDescriptor descriptor, params object[]? messageArgs) {
        Location            = location   ?? throw new ArgumentNullException(nameof(location));
        Descriptor          = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        AdditionalLocations = EmptyAdditionalLocations;
        // `params object[]?` kann vom Aufrufer explizit `null` erhalten (z.B. `new Diagnostic(loc, desc, null)`).
        _messageArgs        = messageArgs ?? EmptyMessageArgs;
    }

    /// <summary>
    /// Erzeugt eine Diagnose an <paramref name="location"/> mit genau einer Zusatz-Position.
    /// </summary>
    /// <param name="location">Die Hauptposition der Diagnose (Datei und Textausschnitt).</param>
    /// <param name="additionalLocation">Eine weitere zur Diagnose gehörende Position (siehe
    /// <see cref="AdditionalLocations"/>).</param>
    /// <param name="descriptor">Die Diagnose-Vorlage (Id, Meldungsformat, Kategorie, Schweregrad).</param>
    /// <param name="messageArgs">
    /// Die Argumente, die die Platzhalter in <see cref="DiagnosticDescriptor.MessageFormat"/> füllen;
    /// darf <c>null</c> sein (wird wie eine leere Argumentliste behandelt).
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="location"/> oder <paramref name="descriptor"/> ist <c>null</c>.
    /// </exception>
    public Diagnostic(Location location, Location additionalLocation, DiagnosticDescriptor descriptor, params object[]? messageArgs)
        : this(location, new[] {additionalLocation}, descriptor, messageArgs) {
    }

    /// <summary>
    /// Erzeugt eine Diagnose an <paramref name="location"/> mit beliebig vielen Zusatz-Positionen.
    /// </summary>
    /// <param name="location">Die Hauptposition der Diagnose (Datei und Textausschnitt).</param>
    /// <param name="additionalLocations">Weitere zur Diagnose gehörende Positionen (siehe
    /// <see cref="AdditionalLocations"/>); <c>null</c>-Einträge werden übersprungen, die Sammlung
    /// selbst darf <c>null</c> sein (wird als leer behandelt).</param>
    /// <param name="descriptor">Die Diagnose-Vorlage (Id, Meldungsformat, Kategorie, Schweregrad).</param>
    /// <param name="messageArgs">
    /// Die Argumente, die die Platzhalter in <see cref="DiagnosticDescriptor.MessageFormat"/> füllen;
    /// darf <c>null</c> sein (wird wie eine leere Argumentliste behandelt).
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="location"/> oder <paramref name="descriptor"/> ist <c>null</c>.
    /// </exception>
    public Diagnostic(Location location, IEnumerable<Location>? additionalLocations, DiagnosticDescriptor descriptor, params object[]? messageArgs) {
        Location            = location                                                          ?? throw new ArgumentNullException(nameof(location));
        Descriptor          = descriptor                                                        ?? throw new ArgumentNullException(nameof(descriptor));
        AdditionalLocations = additionalLocations?.Where(loc => loc != null).ToImmutableArray() ?? EmptyAdditionalLocations;
        // `params object[]?` kann vom Aufrufer explizit `null` erhalten (z.B. `new Diagnostic(loc, desc, null)`).
        _messageArgs        = messageArgs                                                       ?? EmptyMessageArgs;
    }

    /// <summary>
    /// Liefert eine Kopie dieser Diagnose mit <paramref name="location"/> als neuer Hauptposition;
    /// <see cref="Descriptor"/> und Meldungs-Argumente bleiben erhalten, Zusatz-Positionen entfallen.
    /// Wird von <see cref="ExpandLocations"/> genutzt, um jede zugehörige Position zu einer eigenen
    /// Diagnose zu machen.
    /// </summary>
    /// <param name="location">Die neue Hauptposition.</param>
    public Diagnostic WithLocation(Location location) {
        return new Diagnostic(location, Descriptor, _messageArgs);
    }

    static readonly object[]                EmptyMessageArgs         = { };
    static readonly IReadOnlyList<Location> EmptyAdditionalLocations = Enumerable.Empty<Location>().ToImmutableList();

    /// <summary>
    /// Die Hauptposition der Diagnose — Datei und Textausschnitt, auf den sie sich bezieht.
    /// </summary>
    public Location Location { get; }

    /// <summary>
    /// Weitere zur Diagnose gehörende Positionen (z.B. die frühere Deklaration bei einer
    /// Doppel-Deklaration). Leer, wenn keine angegeben wurden.
    /// </summary>
    public IReadOnlyList<Location> AdditionalLocations { get; }

    /// <summary>
    /// Zählt alle Positionen der Diagnose auf — zuerst <see cref="Location"/>, danach die
    /// <see cref="AdditionalLocations"/> in Reihenfolge.
    /// </summary>
    public IEnumerable<Location> GetLocations() {
        yield return Location;

        foreach (var location in AdditionalLocations) {
            yield return location;
        }
    }

    /// <summary>
    /// Fächert die Diagnose in je eine eigene Diagnose pro Position auf: für jede von
    /// <see cref="GetLocations"/> gelieferte Position eine Kopie mit dieser als Hauptposition
    /// (siehe <see cref="WithLocation"/>).
    /// </summary>
    public IEnumerable<Diagnostic> ExpandLocations() {
        return GetLocations().Select(WithLocation);
    }

    /// <summary>
    /// Die Vorlage dieser Diagnose — liefert Identität (<see cref="DiagnosticDescriptor.Id"/>),
    /// <see cref="Category"/>, <see cref="Severity"/> und das Format der <see cref="Message"/>.
    /// </summary>
    public DiagnosticDescriptor Descriptor { get; }
    /// <summary>Der Schweregrad der Diagnose — der <see cref="DiagnosticDescriptor.DefaultSeverity"/> des <see cref="Descriptor"/>.</summary>
    public DiagnosticSeverity   Severity   => Descriptor.DefaultSeverity;
    /// <summary>Die fachliche Einordnung der Diagnose — die <see cref="DiagnosticDescriptor.Category"/> des <see cref="Descriptor"/>.</summary>
    public DiagnosticCategory   Category   => Descriptor.Category;
    /// <summary>Der fertig formatierte Meldungstext — die Platzhalter in <see cref="DiagnosticDescriptor.MessageFormat"/> mit den Meldungs-Argumenten gefüllt.</summary>
    public String               Message    => FormatMessage();

    /// <summary>
    /// Liefert die Textdarstellung dieser Diagnose über den <see cref="DiagnosticFormatter.Instance"/>
    /// (Standard-Format).
    /// </summary>
    public override string ToString() {
        return ToString(null);
    }

    /// <summary>
    /// Liefert die Textdarstellung dieser Diagnose über <paramref name="formatter"/>.
    /// </summary>
    /// <param name="formatter">Der zu verwendende Formatter; <c>null</c> nutzt
    /// <see cref="DiagnosticFormatter.Instance"/>.</param>
    public string ToString(DiagnosticFormatter? formatter) {
        formatter ??= DiagnosticFormatter.Instance;
        return formatter.Format(this);
    }

    #region Equality members

    /// <summary>
    /// Zwei Diagnosen gelten als gleich, wenn sie dieselbe <see cref="Location"/> und denselben
    /// <see cref="Descriptor"/> tragen. Meldungs-Argumente und <see cref="AdditionalLocations"/>
    /// gehen bewusst nicht in den Vergleich ein.
    /// </summary>
    public bool Equals(Diagnostic? other) {
        if (ReferenceEquals(null, other)) {
            return false;
        }

        if (ReferenceEquals(this, other)) {
            return true;
        }

        return Location.Equals(other.Location) && Equals(Descriptor, other.Descriptor);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) {
        if (ReferenceEquals(null, obj)) {
            return false;
        }

        if (ReferenceEquals(this, obj)) {
            return true;
        }

        return obj is Diagnostic diagnostic && Equals(diagnostic);
    }

    /// <summary>
    /// Liefert einen Hashcode, der zur Gleichheit passt — abgeleitet aus <see cref="Location"/> und
    /// <see cref="Descriptor"/>.
    /// </summary>
    public override int GetHashCode() {
        unchecked {
            return (Location.GetHashCode() * 397) ^ Descriptor.GetHashCode();
        }
    }

    /// <summary>Prüft zwei Diagnosen auf Gleichheit (siehe <see cref="Equals(Diagnostic)"/>).</summary>
    public static bool operator ==(Diagnostic? left, Diagnostic? right) {
        return Equals(left, right);
    }

    /// <summary>Prüft zwei Diagnosen auf Ungleichheit (siehe <see cref="Equals(Diagnostic)"/>).</summary>
    public static bool operator !=(Diagnostic? left, Diagnostic? right) {
        return !Equals(left, right);
    }

    #endregion

    string FormatMessage() {
        if (_messageArgs.Length != 0) {
            return String.Format(Descriptor.MessageFormat, _messageArgs);
        } else {
            return Descriptor.MessageFormat;
        }
    }

}