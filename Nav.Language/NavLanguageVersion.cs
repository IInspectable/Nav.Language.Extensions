#nullable enable

using System;
using System.Collections.Immutable;
using System.Globalization;

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Die Sprach-/Schema-Version einer <c>.nav</c>-Datei — eine monotone Ganzzahl (<c>1</c>, <c>2</c>,
/// <c>3</c> …), gesteuert per <c>#version</c>. Sie ist bewusst von der (git-abgeleiteten)
/// Assembly-Version entkoppelt und beschreibt, <i>welche</i> Generation von Nav-Syntax- und
/// Codegen-Elementen in einer Datei gilt. Fehlt das Pragma, gilt <see cref="Default"/> (Version 1 —
/// das historische Sprachverhalten, unter dem der gesamte Bestand byte-identisch übersetzt).
/// </summary>
[Serializable]
public readonly struct NavLanguageVersion: IEquatable<NavLanguageVersion>, IComparable<NavLanguageVersion> {

    /// <summary>Erzeugt eine Version mit ihrem numerischen Wert.</summary>
    public NavLanguageVersion(int value) {
        Value = value;
    }

    /// <summary>Der numerische Wert der Version (<c>1</c>, <c>2</c>, <c>3</c> …).</summary>
    public int Value { get; }

    // -- Bekannte Sprach-Versionen -------------------------------------------------------------------------
    // Einzige Autorität für die von der Engine unterstützten Versionen. Jede Version ist eine benannte
    // Konstante; im übrigen Code stehen keine „magischen" Versionszahlen. Eine neue Version freizuschalten
    // heißt: hier eine weitere Konstante anlegen und in SupportedVersions aufnehmen.

    /// <summary>
    /// Version 1 — das historische Sprachverhalten, unter dem der gesamte Bestand byte-identisch übersetzt.
    /// Gilt auch, wenn kein <c>#version</c> vorhanden ist (siehe <see cref="Default"/>).
    /// </summary>
    public static NavLanguageVersion Version1 { get; } = new(1);

    /// <summary>
    /// Alle von der Engine unterstützten Sprach-Versionen, aufsteigend. Einzige Quelle der Wahrheit für die
    /// Gültigkeitsprüfung (<see cref="IsSupported"/>) und für <see cref="Latest"/>.
    /// </summary>
    public static ImmutableArray<NavLanguageVersion> SupportedVersions => SupportedVersionTable.All;

    /// <summary>
    /// Die Version, die ohne <c>#version</c> gilt — das historische Sprachverhalten
    /// (<see cref="Version1"/>).
    /// </summary>
    public static NavLanguageVersion Default => Version1;

    /// <summary>Die höchste derzeit von der Engine unterstützte Sprach-Version.</summary>
    public static NavLanguageVersion Latest => SupportedVersionTable.All[SupportedVersionTable.All.Length - 1];

    /// <summary>
    /// Ob diese Version von der Engine unterstützt wird — also in <see cref="SupportedVersions"/> enthalten
    /// ist. Ein wohlgeformter, aber unbekannter Wert (z.B. <c>#version 99</c>) liefert <c>false</c>
    /// und wird semantisch als <c>Nav5001</c> gemeldet.
    /// </summary>
    public bool IsSupported => SupportedVersionTable.All.Contains(this);

    /// <summary>
    /// Parst eine reine, nicht-negative Ganzzahl (z.B. <c>"2"</c>) in eine <see cref="NavLanguageVersion"/>.
    /// Umschließende Leerzeichen sind zulässig; alles andere (leer, Vorzeichen, Nicht-Ziffern, Überlauf)
    /// schlägt fehl und liefert <c>false</c>.
    /// </summary>
    public static bool TryParse(string? text, out NavLanguageVersion version) {

        version = default;

        if (text == null) {
            return false;
        }

        var trimmed = text.Trim();
        if (trimmed.Length == 0) {
            return false;
        }

        foreach (var c in trimmed) {
            if (c < '0' || c > '9') {
                return false;
            }
        }

        if (!int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out var value)) {
            return false;
        }

        version = new NavLanguageVersion(value);
        return true;
    }

    public int CompareTo(NavLanguageVersion other) => Value.CompareTo(other.Value);
    public bool Equals(NavLanguageVersion other) => Value == other.Value;

    public override bool Equals(object? obj) => obj is NavLanguageVersion other && Equals(other);
    public override int GetHashCode() => Value;
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    public static bool operator ==(NavLanguageVersion left, NavLanguageVersion right) => left.Equals(right);
    public static bool operator !=(NavLanguageVersion left, NavLanguageVersion right) => !left.Equals(right);
    public static bool operator <(NavLanguageVersion left, NavLanguageVersion right) => left.Value  < right.Value;
    public static bool operator >(NavLanguageVersion left, NavLanguageVersion right) => left.Value  > right.Value;
    public static bool operator <=(NavLanguageVersion left, NavLanguageVersion right) => left.Value <= right.Value;
    public static bool operator >=(NavLanguageVersion left, NavLanguageVersion right) => left.Value >= right.Value;

}

/// <summary>
/// Halter für die Liste der unterstützten Versionen. Bewusst eine eigene (Referenz-)Klasse und <b>nicht</b>
/// ein statisches Feld in <see cref="NavLanguageVersion"/> selbst: ein Wertetyp mit einem statischen Feld
/// einer Generic-Instanz über sich selbst (<c>ImmutableArray&lt;NavLanguageVersion&gt;</c>) lädt der
/// .NET-Framework-Typlader nicht (<c>TypeLoadException</c>, empirisch auf net472 verifiziert; .NET 10 ist
/// toleranter). Der separate Halter umgeht das, ohne die öffentliche API zu verändern.
/// </summary>
static class SupportedVersionTable {

    public static readonly ImmutableArray<NavLanguageVersion> All = ImmutableArray.Create(
        NavLanguageVersion.Version1);

}