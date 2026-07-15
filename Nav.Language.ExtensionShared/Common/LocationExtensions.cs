#region Using Directives

using Microsoft.VisualStudio.Text;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Common; 

/// <summary>
/// Erweiterungsmethoden, die eine Nav-<see cref="Location"/> bzw. deren <see cref="TextExtent"/> in
/// die Span-Typen des VS-Editors (<see cref="SnapshotSpan"/>, <see cref="Span"/>) übersetzen.
/// </summary>
static class LocationExtensions {

    /// <summary>
    /// Übersetzt die <see cref="Location"/> in einen <see cref="SnapshotSpan"/> über dem
    /// angegebenen Snapshot.
    /// </summary>
    /// <param name="location">Die zu übersetzende Location.</param>
    /// <param name="textSnapshot">Der Snapshot, auf den der Span bezogen wird.</param>
    /// <returns>Der entsprechende <see cref="SnapshotSpan"/>.</returns>
    public static SnapshotSpan ToSnapshotSpan(this Location location, ITextSnapshot textSnapshot) {
        return location.Extent.ToSnapshotSpan(textSnapshot);
    }

    /// <summary>
    /// Übersetzt einen <see cref="TextExtent"/> in einen <see cref="SnapshotSpan"/> über dem
    /// angegebenen Snapshot.
    /// </summary>
    /// <param name="extent">Der zu übersetzende Bereich.</param>
    /// <param name="textSnapshot">Der Snapshot, auf den der Span bezogen wird.</param>
    /// <returns>Der entsprechende <see cref="SnapshotSpan"/>.</returns>
    public static SnapshotSpan ToSnapshotSpan(this TextExtent extent, ITextSnapshot textSnapshot) {
        // TODO Adaption von Start und Legth
        return new SnapshotSpan(textSnapshot, start: extent.Start, length: extent.Length);
    }

    /// <summary>
    /// Übersetzt einen <see cref="TextExtent"/> in einen snapshot-unabhängigen <see cref="Span"/>.
    /// </summary>
    /// <param name="extent">Der zu übersetzende Bereich.</param>
    /// <returns>Der entsprechende <see cref="Span"/>.</returns>
    public static Span ToSpan(this TextExtent extent) {
        return new Span(start: extent.Start, length: extent.Length);
    }
}