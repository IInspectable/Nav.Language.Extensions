﻿#region Using Directives

using Microsoft.VisualStudio.Text;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Common; 

static class LocationExtensions {

    public static SnapshotSpan ToSnapshotSpan(this Location location, ITextSnapshot textSnapshot) {
        return location.Extent.ToSnapshotSpan(textSnapshot);
    }

    public static SnapshotSpan ToSnapshotSpan(this TextExtent extent, ITextSnapshot textSnapshot) {
        // TODO Adaption von Start und Legth
        return new SnapshotSpan(textSnapshot, start: extent.Start, length: extent.Length);
    }

    public static Span ToSpan(this TextExtent extent) {
        return new Span(start: extent.Start, length: extent.Length);
    }
}