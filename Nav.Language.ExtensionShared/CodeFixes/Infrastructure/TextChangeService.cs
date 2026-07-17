#region Using Directives

using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;

using Pharmatechnik.Nav.Language.Extension.Common;
using Pharmatechnik.Nav.Language.Extension.Utilities;

using TextExtent = Pharmatechnik.Nav.Language.Text.TextExtent;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.CodeFixes; 

/// <summary>
/// Der Dienst, der ein von einem Fix berechnetes Edit-Set auf den Editor anwendet — die Brücke von den
/// VS-freien Engine-<see cref="Pharmatechnik.Nav.Language.Text.TextChange"/>s zu den VS-Editor-Operationen
/// (<see cref="ITextEdit"/>, Undo-Transaktion, Warteanzeige).
/// </summary>
interface ITextChangeService {
    /// <summary>
    /// Wendet die Änderungen aus <paramref name="textChangesAndSnapshot"/> als eine widerrufbare Bearbeitung
    /// auf den <paramref name="textView"/> an.
    /// </summary>
    /// <param name="textView">Der Editor-View, dessen Puffer bearbeitet wird.</param>
    /// <param name="undoDescription">Die Beschreibung des Vorgangs in der Undo-Historie.</param>
    /// <param name="textChangesAndSnapshot">Das Edit-Set samt zugehörigem Ursprungs-Snapshot.</param>
    /// <param name="waitMessage">Die Warteanzeige-Meldung; standardmäßig <paramref name="undoDescription"/>.</param>
    /// <returns>Der resultierende <see cref="ITextSnapshot"/> nach dem Anwenden.</returns>
    ITextSnapshot ApplyTextChanges(ITextView textView, string undoDescription, TextChangesAndSnapshot textChangesAndSnapshot, string waitMessage = null);
}
   
/// <summary>Die über MEF exportierte Standard-Implementierung von <see cref="ITextChangeService"/>.</summary>
[Export(typeof(ITextChangeService))]
class TextChangeService: ITextChangeService {

    readonly IWaitIndicator                  _waitIndicator;
    readonly ITextUndoHistoryRegistry        _undoHistoryRegistry;
    readonly IEditorOperationsFactoryService _editorOperationsFactoryService;
        
    /// <summary>Importiert die zum Anwenden benötigten Editor-Dienste über MEF.</summary>
    /// <param name="waitIndicator">Zeigt während der Bearbeitung eine Warteanzeige.</param>
    /// <param name="undoHistoryRegistry">Liefert die Undo-Historie des Puffers für die Transaktion.</param>
    /// <param name="editorOperationsFactoryService">Erzeugt die Editor-Operationen für die Undo-Transaktion.</param>
    [ImportingConstructor]
    public TextChangeService(IWaitIndicator waitIndicator,
                             ITextUndoHistoryRegistry undoHistoryRegistry,
                             IEditorOperationsFactoryService editorOperationsFactoryService) {

        _waitIndicator                  = waitIndicator;
        _undoHistoryRegistry            = undoHistoryRegistry;
        _editorOperationsFactoryService = editorOperationsFactoryService;
    }

    /// <summary>
    /// Wendet die Änderungen in einer einzigen <see cref="ITextEdit"/>-Bearbeitung an, eingefasst in eine
    /// Warteanzeige und eine committete Undo-Transaktion (<see cref="TextUndoTransaction"/>), sodass der
    /// gesamte Fix in einem Schritt widerrufbar ist. Jede Änderung wird über
    /// <see cref="TranslateToTextEditSpan"/> auf den aktuellen Snapshot nachgeführt.
    /// </summary>
    /// <param name="textView">Der Editor-View, dessen Puffer bearbeitet wird.</param>
    /// <param name="undoDescription">Die Beschreibung des Vorgangs in der Undo-Historie.</param>
    /// <param name="textChangesAndSnapshot">Das Edit-Set samt zugehörigem Ursprungs-Snapshot.</param>
    /// <param name="waitMessage">Die Warteanzeige-Meldung; standardmäßig <paramref name="undoDescription"/>.</param>
    /// <returns>Der resultierende <see cref="ITextSnapshot"/> nach dem Anwenden.</returns>
    public ITextSnapshot ApplyTextChanges(ITextView textView, string undoDescription, TextChangesAndSnapshot textChangesAndSnapshot, string waitMessage=null) {

        waitMessage ??= undoDescription;

        using (_waitIndicator.StartWait(undoDescription, waitMessage, allowCancel: false))
        using (var undoTransaction = new TextUndoTransaction(undoDescription, textView, _undoHistoryRegistry, _editorOperationsFactoryService))
        using (var textEdit = textView.TextBuffer.CreateEdit()) {

            foreach (var change in textChangesAndSnapshot.TextChanges) {
                var span = TranslateToTextEditSpan(textChangesAndSnapshot.Snapshot, change.Extent, textEdit);
                textEdit.Replace(span, change.ReplacementText);
            }

            var textSnapshot =textEdit.Apply();

            undoTransaction.Commit();

            return textSnapshot;
        }
    }

    /// <summary>
    /// Übersetzt einen Engine-<see cref="TextExtent"/> vom Ursprungs-Snapshot in einen
    /// <see cref="SnapshotSpan"/> des aktuellen Bearbeitungs-Snapshots, indem er über einen
    /// EdgeInclusive-Tracking-Span nachgeführt wird — falls der Puffer sich seit der semantischen Analyse
    /// verändert hat.
    /// </summary>
    /// <param name="sourceSnapshot">Der Snapshot, auf dem der Bereich berechnet wurde.</param>
    /// <param name="extent">Der zu übersetzende Bereich.</param>
    /// <param name="textEdit">Die laufende Bearbeitung, deren Snapshot das Ziel ist.</param>
    /// <returns>Der auf den aktuellen Snapshot nachgeführte Bereich.</returns>
    SnapshotSpan TranslateToTextEditSpan(ITextSnapshot sourceSnapshot, TextExtent extent, ITextEdit textEdit) {
        // Theoretisch kann es sein, das der Snapshot, auf dem die Semantic Anayse gelaufen ist,
        // nicht mit dem aktuellen Snaphot des textEdits übereinstimmt.
        // Ob es nun Sinn macht, den SnapshotSpan auf den aktuellen TextSnapshot zu transformieren,
        // oder ob es nicht eigentlich besser wäre, die ganze Aktion abzubrechen, wird die Zeit zeigen.
        var snapshotSpan = GetSnapshotSpan(extent, sourceSnapshot);
        var trackingSpan = sourceSnapshot.CreateTrackingSpan(snapshotSpan, SpanTrackingMode.EdgeInclusive);
        var targetSpan   = trackingSpan.GetSpan(textEdit.Snapshot);
        return targetSpan;
    }

    /// <summary>Bildet einen <see cref="TextExtent"/> direkt (ohne Nachführen) auf einen <see cref="SnapshotSpan"/> des angegebenen Snapshots ab.</summary>
    /// <param name="extent">Der abzubildende Bereich.</param>
    /// <param name="snapshot">Der Ziel-Snapshot.</param>
    /// <returns>Der entsprechende <see cref="SnapshotSpan"/>.</returns>
    SnapshotSpan GetSnapshotSpan(TextExtent extent, ITextSnapshot snapshot) {
        return new SnapshotSpan(snapshot, start: extent.Start, length: extent.Length);
    }
}