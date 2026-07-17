#region Using Directives

using System.Collections.Generic;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Common; 

/// <summary>
/// Erweiterungsmethoden über <see cref="ITextSelection"/>, die die Auswahl auf einen bestimmten
/// Puffer der Puffer-Hierarchie herunterprojizieren.
/// </summary>
static class TextSelectionExtensions {

    /// <summary>
    /// Bildet die ausgewählten Spans über den <c>BufferGraph</c> nach <paramref name="subjectBuffer"/>
    /// ab und liefert sie als normalisierte Sammlung (leer, wenn die Auswahl nicht in den Zielpuffer
    /// fällt).
    /// </summary>
    /// <param name="selection">Die Auswahl in der Ansicht.</param>
    /// <param name="subjectBuffer">Der Zielpuffer, auf den heruntergemappt wird.</param>
    /// <returns>Die auf den Zielpuffer projizierten, normalisierten Spans.</returns>
    public static NormalizedSnapshotSpanCollection GetSnapshotSpansOnBuffer(this ITextSelection selection, ITextBuffer subjectBuffer) {
        var list = new List<SnapshotSpan>();
        foreach(var snapshotSpan in selection.SelectedSpans) {
            list.AddRange(selection.TextView.BufferGraph.MapDownToBuffer(snapshotSpan, SpanTrackingMode.EdgeExclusive, subjectBuffer));
        }
        return new NormalizedSnapshotSpanCollection(list);
    }
}