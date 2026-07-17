#region Using Directives

using Microsoft.VisualStudio.Text;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Common; 

/// <summary>
/// Erweiterungsmethoden, die die <see cref="Location"/> eines Nav-<see cref="ISymbol"/> in einen
/// <see cref="SnapshotSpan"/> des angegebenen <see cref="ITextSnapshot"/> übersetzen — die Brücke
/// zwischen der zeichenbasierten Location des Semantikmodells und der VS-Text-Ebene.
/// </summary>
static class SymbolExtensions {

    /// <summary>
    /// Übersetzt eine <see cref="Location"/> in den entsprechenden <see cref="SnapshotSpan"/> von
    /// <paramref name="snapshot"/>.
    /// </summary>
    /// <param name="location">Die zeichenbasierte Location.</param>
    /// <param name="snapshot">Der Ziel-Snapshot.</param>
    /// <returns>Der zur Location gehörende Span im Snapshot.</returns>
    public static SnapshotSpan GetSnapshotSpan(this Location location, ITextSnapshot snapshot) {
        return location.ToSnapshotSpan(snapshot);
    }
        
    /// <summary>
    /// Übersetzt die <see cref="ISymbol.Location"/> von <paramref name="symbol"/> in den
    /// entsprechenden <see cref="SnapshotSpan"/> von <paramref name="snapshot"/>.
    /// </summary>
    /// <param name="symbol">Das Nav-Symbol, dessen Location abgebildet wird.</param>
    /// <param name="snapshot">Der Ziel-Snapshot.</param>
    /// <returns>Der zur Symbol-Location gehörende Span im Snapshot.</returns>
    public static SnapshotSpan GetSnapshotSpan(this ISymbol symbol, ITextSnapshot snapshot) {
        return GetSnapshotSpan(symbol.Location, snapshot);
    }
}