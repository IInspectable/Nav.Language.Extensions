#region Using Directives

using System;
using System.Linq;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Projection;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Common; 

/// <summary>
/// Erweiterungsmethoden für den <see cref="IBufferGraph"/>, der die Projektionsbeziehungen
/// zwischen Text-Puffern abbildet.
/// </summary>
static class BufferGraphExtensions {

    /// <summary>
    /// Bildet den übergebenen <see cref="SnapshotSpan"/> im Puffer-Graphen auf den ersten
    /// passenden Snapshot ab — zunächst abwärts (in Quell-Puffer), andernfalls aufwärts (in
    /// Projektions-Puffer).
    /// </summary>
    /// <param name="bufferGraph">Der Puffer-Graph, über den abgebildet wird.</param>
    /// <param name="span">Der abzubildende Bereich.</param>
    /// <param name="match">Prädikat, das den Zielsnapshot auswählt.</param>
    /// <returns>
    /// Der erste passende <see cref="SnapshotSpan"/> oder <c>null</c>, wenn kein Snapshot passt.
    /// </returns>
    public static SnapshotSpan? MapUpOrDownToFirstMatch(this IBufferGraph bufferGraph, SnapshotSpan span, Predicate<ITextSnapshot> match) {
        var spans = bufferGraph.MapDownToFirstMatch(span, SpanTrackingMode.EdgeExclusive, match);
        if(!spans.Any()) {
            spans = bufferGraph.MapUpToFirstMatch(span, SpanTrackingMode.EdgeExclusive, match);
        }

        return spans.FirstOrDefault();
    }        
}