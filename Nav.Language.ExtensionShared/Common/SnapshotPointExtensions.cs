#region Using Directives

using Microsoft.VisualStudio.Text;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Common; 

/// <summary>
/// Erweiterungsmethoden, die einen <see cref="SnapshotPoint"/> in einen <see cref="SnapshotSpan"/>
/// überführen und dabei sicherstellen, dass der Span innerhalb des zugehörigen
/// <see cref="ITextSnapshot"/> liegt (nötig für Tagging/Adorner an der Puffer-Grenze).
/// </summary>
static class SnapshotPointExtensions {

    /// <summary>
    /// Bildet um <paramref name="snapshotPoint"/> einen <see cref="SnapshotSpan"/> der Länge
    /// <paramref name="length"/>. Ragt der Span über das Snapshot-Ende hinaus, wird der Start so weit
    /// nach vorne verschoben, dass der Span vollständig im Snapshot liegt.
    /// </summary>
    /// <param name="snapshotPoint">Der Ausgangspunkt im Snapshot.</param>
    /// <param name="length">Die gewünschte Länge des Spans; Vorgabe 0.</param>
    /// <returns>Der auf den Snapshot beschränkte Span.</returns>
    public static SnapshotSpan ToSnapshotSpan(this SnapshotPoint snapshotPoint, int length=0) {
        var start = snapshotPoint.Position;
        if (length > 0 && snapshotPoint.Snapshot.Length >=length) {
            var offfset = start + length - snapshotPoint.Snapshot.Length;
            if (offfset >0) {
                start -= offfset;
            }
        }
        return new SnapshotSpan(snapshotPoint.Snapshot, start, length);
    }

    /// <summary>
    /// Bildet um <paramref name="snapshotPoint"/> einen <see cref="SnapshotSpan"/> der Länge 1 und
    /// hält ihn dabei innerhalb des Snapshots (Sonderfall von <see cref="ToSnapshotSpan(SnapshotPoint,int)"/>
    /// für ein einzelnes Zeichen, etwa zum Treffen des Zeichens unter dem Caret).
    /// </summary>
    /// <param name="snapshotPoint">Der Ausgangspunkt im Snapshot.</param>
    /// <returns>Der ein Zeichen breite, auf den Snapshot beschränkte Span.</returns>
    public static SnapshotSpan ExtendToLength1(this SnapshotPoint snapshotPoint) {
        var start = snapshotPoint.Position;
        if(snapshotPoint.Snapshot.Length >= 1) {
            var offset = start + 1 - snapshotPoint.Snapshot.Length;
            if(offset > 0) {
                start -= offset;
            }
                
        }
        return new SnapshotSpan(snapshotPoint.Snapshot, start, 1);
    }

    /// <summary>
    /// Nullbare Variante von <see cref="ToSnapshotSpan(SnapshotPoint,int)"/>: liefert
    /// <see langword="null"/>, wenn <paramref name="snapshotPoint"/> keinen Wert hat.
    /// </summary>
    /// <param name="snapshotPoint">Der optionale Ausgangspunkt im Snapshot.</param>
    /// <returns>Der Span der Länge 0, oder <see langword="null"/>.</returns>
    public static SnapshotSpan? ToSnapshotSpan(this SnapshotPoint? snapshotPoint) {
        return snapshotPoint?.ToSnapshotSpan();
    }
}