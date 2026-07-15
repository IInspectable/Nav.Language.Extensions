#region Using Directives

using System;

using Pharmatechnik.Nav.Language.Text;
using Pharmatechnik.Nav.Utilities.IO;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Die Verortung eines Textausschnitts — vereint den zeichenbasierten <see cref="Extent"/> (Start und
/// Länge, siehe <see cref="TextExtent"/>) mit der zeilen-/spaltenbasierten Position
/// (<see cref="StartLinePosition"/>/<see cref="EndLinePosition"/>) und optional dem
/// <see cref="FilePath"/> der Quelldatei. Sie ist das gemeinsame „Wo" von Symbolen, Diagnosen und
/// Navigationszielen. Zeilen und Spalten sind 0-basiert. Roslyn-Analogon
/// <c>Microsoft.CodeAnalysis.Location</c> bzw. <c>FileLinePositionSpan</c>.
/// </summary>
public class Location: IEquatable<Location> {

    string? _normalizedFilePath;

    /// <summary>
    /// Kopierkonstruktor — übernimmt Ausschnitt, Positionen, Dateipfad und den bereits berechneten
    /// normalisierten Pfad unverändert. Ableitungshook für spezialisierte <see cref="Location"/>-Arten.
    /// </summary>
    /// <param name="location">Die zu kopierende Verortung.</param>
    protected Location(Location location) {
        Extent              = location.Extent;
        StartLinePosition   = location.StartLinePosition;
        EndLinePosition     = location.EndLinePosition;
        FilePath            = location.FilePath;
        _normalizedFilePath = location._normalizedFilePath;
    }

    /// <summary>
    /// Erzeugt eine Verortung aus Textausschnitt und Zeilenbereich.
    /// </summary>
    /// <param name="extent">Der zeichenbasierte Ausschnitt (Start und Länge).</param>
    /// <param name="lineRange">Der zugehörige Zeilen-/Spaltenbereich.</param>
    /// <param name="filePath">Der Pfad der Quelldatei; darf <c>null</c> sein.</param>
    public Location(TextExtent extent, LineRange lineRange, string? filePath) {
        Extent            = extent;
        StartLinePosition = lineRange.Start;
        EndLinePosition   = lineRange.End;
        FilePath          = filePath;
    }

    /// <summary>
    /// Erzeugt eine Verortung, deren Start- und Endzeile auf derselben <paramref name="linePosition"/>
    /// liegen.
    /// </summary>
    /// <param name="extent">Der zeichenbasierte Ausschnitt (Start und Länge).</param>
    /// <param name="linePosition">Die Zeilen-/Spaltenposition für Anfang und Ende.</param>
    /// <param name="filePath">Der Pfad der Quelldatei; darf <c>null</c> sein.</param>
    public Location(TextExtent extent, LinePosition linePosition, string? filePath):
        this(extent, new LineRange(linePosition, linePosition), filePath) {
    }

    /// <summary>
    /// Erzeugt eine leere Verortung, die nur eine Datei benennt — Ausschnitt und Positionen sind leer
    /// (<see cref="TextExtent.Empty"/>/<see cref="LinePosition.Empty"/>).
    /// </summary>
    /// <param name="filePath">Der Pfad der Quelldatei; darf <c>null</c> sein.</param>
    public Location(string? filePath) {
        Extent            = TextExtent.Empty;
        StartLinePosition = LinePosition.Empty;
        EndLinePosition   = LinePosition.Empty;
        FilePath          = filePath;
    }

    //TODO Missing/None

    /// <summary>Der zeichenbasierte Textausschnitt (Start und Länge) der Verortung.</summary>
    public TextExtent   Extent            { get; }
    /// <summary>Die Zeilen-/Spaltenposition des Anfangs (0-basiert).</summary>
    public LinePosition StartLinePosition { get; }
    /// <summary>Die Zeilen-/Spaltenposition des Endes (0-basiert).</summary>
    public LinePosition EndLinePosition   { get; }
    /// <summary>Der Zeilenbereich der Verortung — <see cref="StartLinePosition"/> bis <see cref="EndLinePosition"/>.</summary>
    public LineRange    LineRange         => new(StartLinePosition, EndLinePosition);

    /// <summary>
    /// Der Pfad der Quelldatei oder <c>null</c>.
    /// </summary>
    public string? FilePath { get; }

    /// <summary>
    /// Der normalisierte <see cref="FilePath"/> (für pfadunabhängige Vergleiche, z.B. in der Gleichheit);
    /// <c>null</c>, wenn kein Pfad vorliegt. Wird beim ersten Zugriff berechnet und zwischengespeichert.
    /// </summary>
    public string? NormalizedFilePath => FilePath == null ? null : _normalizedFilePath ??= PathHelper.NormalizePath(FilePath);

    /// <summary>
    /// Der Start-Zeichenindex der Verortung [0..n]; <c>-1</c>, wenn unbekannt/abwesend.
    /// </summary>
    public int Start => Extent.Start;

    /// <summary>
    /// Die Startzeile der Verortung. Die erste Zeile einer Datei ist Zeile 0 (0-basierte Zählung).
    /// </summary>
    public int StartLine => StartLinePosition.Line;

    /// <summary>
    /// Die Spaltenposition innerhalb der Startzeile (0-basiert).
    /// </summary>
    public int StartCharacter => StartLinePosition.Character;

    /// <summary>
    /// Die Länge der Verortung; garantiert größer oder gleich 0.
    /// </summary>
    public int Length => Extent.Length;

    /// <summary>
    /// Der End-Zeichenindex der Verortung (0-basiert); zeigt tatsächlich auf das Zeichen <i>hinter</i>
    /// dem Ende der Verortung.
    /// </summary>
    public int End => Extent.End;

    /// <summary>
    /// Die Endzeile der Verortung. Die erste Zeile einer Datei ist Zeile 0 (0-basierte Zählung).
    /// </summary>
    public int EndLine => EndLinePosition.Line;

    /// <summary>
    /// Die Spaltenposition innerhalb der Endzeile (0-basiert).
    /// </summary>
    public int EndCharacter => EndLinePosition.Character;

    /// <summary>
    /// Liefert eine kompakte Textdarstellung <c>&lt;Pfad&gt;@&lt;Zeile&gt;:&lt;Spalte&gt;</c> mit
    /// 1-basierter Zeile und Spalte (v.a. für Diagnose-/Debug-Zwecke).
    /// </summary>
    public override string ToString() {
        return $"{FilePath}@{StartLine + 1}:{StartCharacter + 1}";
    }

    #region Equality members

    /// <summary>
    /// Zwei Verortungen gelten als gleich, wenn <see cref="Extent"/>, <see cref="StartLinePosition"/>,
    /// <see cref="EndLinePosition"/> und der <see cref="NormalizedFilePath"/> übereinstimmen (Pfade
    /// werden also normalisiert verglichen).
    /// </summary>
    public bool Equals(Location? other) {
        if (ReferenceEquals(null, other)) {
            return false;
        }

        if (ReferenceEquals(this, other)) {
            return true;
        }

        return Extent.Equals(other.Extent)                       &&
               StartLinePosition.Equals(other.StartLinePosition) &&
               EndLinePosition.Equals(other.EndLinePosition)     &&
               string.Equals(NormalizedFilePath, other.NormalizedFilePath);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) {
        if (ReferenceEquals(null, obj)) {
            return false;
        }

        if (ReferenceEquals(this, obj)) {
            return true;
        }

        return obj is Location location && Equals(location);
    }

    /// <summary>
    /// Liefert einen zur Gleichheit passenden Hashcode aus <see cref="Extent"/>,
    /// <see cref="StartLinePosition"/>, <see cref="EndLinePosition"/> und <see cref="NormalizedFilePath"/>.
    /// </summary>
    public override int GetHashCode() {
        unchecked {
            var hashCode = Extent.GetHashCode();
            hashCode = (hashCode * 397) ^ StartLinePosition.GetHashCode();
            hashCode = (hashCode * 397) ^ EndLinePosition.GetHashCode();
            hashCode = (hashCode * 397) ^ (NormalizedFilePath?.GetHashCode() ?? 0);
            return hashCode;
        }
    }

    /// <summary>Prüft zwei Verortungen auf Gleichheit (siehe <see cref="Equals(Location)"/>).</summary>
    public static bool operator ==(Location? left, Location? right) {
        return Equals(left, right);
    }

    /// <summary>Prüft zwei Verortungen auf Ungleichheit (siehe <see cref="Equals(Location)"/>).</summary>
    public static bool operator !=(Location? left, Location? right) {
        return !Equals(left, right);
    }

    #endregion

        

}