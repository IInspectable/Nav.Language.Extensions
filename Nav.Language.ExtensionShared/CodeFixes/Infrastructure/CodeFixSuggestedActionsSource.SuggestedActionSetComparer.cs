#region Using Directives

using System.Collections.Generic;

using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.CodeFixes; 

partial class CodeFixSuggestedActionsSource {

    /// <summary>
    /// Ordnet <see cref="SuggestedActionSet"/>s nach ihrer Nähe zum Auslösepunkt (dem Caret), damit der
    /// Editor die dem Cursor nächsten Vorschläge zuerst zeigt. Sets, deren Anker-Bereich den Auslösepunkt
    /// enthält, gelten als am nächsten; bei Gleichstand entscheidet zuerst der Abstand zum Bereichs-Anfang,
    /// dann zum Bereichs-Ende. Ohne Auslösepunkt ist keine Ordnung möglich (alle gleich).
    /// </summary>
    sealed class SuggestedActionSetComparer : IComparer<SuggestedActionSet> {

        readonly SnapshotPoint? _triggerPoint;
        readonly SnapshotSpan   _defaultSpan;

        /// <summary>Erzeugt den Vergleicher für einen Auslösepunkt und einen Ersatz-Bereich.</summary>
        /// <param name="triggerPoint">Der Auslösepunkt (Caret), oder <c>null</c>, wenn keiner vorliegt.</param>
        /// <param name="defaultSpan">Der Ersatz-Bereich für Sets ohne eigenen Anker-Bereich.</param>
        public SuggestedActionSetComparer(SnapshotPoint? triggerPoint, SnapshotSpan defaultSpan) {
                
            _triggerPoint = triggerPoint;
            _defaultSpan  = defaultSpan;
        }

        /// <summary>
        /// Berechnet den Abstand des Auslösepunkts zum Bereich: 0, wenn er darin liegt, sonst die Distanz zur
        /// nächsten Kante. Ohne Auslösepunkt <see cref="int.MaxValue"/>.
        /// </summary>
        /// <param name="span">Der Bereich, dessen Abstand ermittelt wird.</param>
        /// <returns>Der Abstand in Zeichen (0 = enthält den Punkt).</returns>
        int Distance(Span span) {
            // If we don't have a text span or target point we cannot calculate the distance between them
            if(_triggerPoint == null) {
                return int.MaxValue;
            }

            var position = _triggerPoint.Value.Position;

            if(position < span.Start) {
                return span.Start - position;
            } else if(position > span.End) {
                return position - span.End;
            } else {
                return 0;
            }
        }

        /// <summary>
        /// Vergleicht zwei Sets nach Caret-Nähe: zuerst nach <see cref="Distance"/>; enthalten beide den
        /// Auslösepunkt, nach Abstand zum Bereichs-Anfang, bei Gleichstand nach Abstand zum Bereichs-Ende.
        /// Ohne Auslösepunkt gelten alle als gleich.
        /// </summary>
        /// <param name="x">Das erste Set (Ersatz-Bereich, wenn ohne Anker).</param>
        /// <param name="y">Das zweite Set (Ersatz-Bereich, wenn ohne Anker).</param>
        /// <returns>Negativ/0/positiv nach der üblichen <see cref="IComparer{T}.Compare"/>-Konvention.</returns>
        public int Compare(SuggestedActionSet x, SuggestedActionSet y) {
            var triggerPoint = _triggerPoint;
            if (triggerPoint == null) {
                // Ohne Triggerpoint kann keine Aussage getroffen werden
                return 0;
            }

            var xSpan = x?.ApplicableToSpan ?? _defaultSpan.Span;
            var ySpan = y?.ApplicableToSpan ?? _defaultSpan.Span;

            var distanceX = Distance(xSpan);
            var distanceY = Distance(ySpan);

            if(distanceX != 0 || distanceY != 0) {
                return distanceX.CompareTo(distanceY);
            }

            // This is the case when both actions sets' spans contain the trigger point.
            // Now we compare first by start position then by end position. 
            var triggerPosition = triggerPoint.Value.Position;

            var distanceToStartX = triggerPosition - xSpan.Start;
            var distanceToStartY = triggerPosition - ySpan.Start;

            if(distanceToStartX != distanceToStartY) {
                return distanceToStartX.CompareTo(distanceToStartY);
            }

            var distanceToEndX = xSpan.End - triggerPosition;
            var distanceToEndY = ySpan.End - triggerPosition;

            return distanceToEndX.CompareTo(distanceToEndY);
        }
    }
}