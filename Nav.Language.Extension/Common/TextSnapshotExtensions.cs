﻿using Microsoft.VisualStudio.Text;

namespace Pharmatechnik.Nav.Language.Extension.Common {
    static class TextSnapshotExtensions {

        public static SnapshotSpan GetFullSpan(this ITextSnapshot snapshot) {
           return new SnapshotSpan(snapshot, 0, snapshot.Length);
        }
    }
}