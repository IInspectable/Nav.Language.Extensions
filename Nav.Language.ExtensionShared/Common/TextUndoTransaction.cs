#region Using Directives

using System;
using JetBrains.Annotations;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Common; 

/// <summary>
/// Kapselt eine <see cref="ITextUndoTransaction"/> als <see cref="IDisposable"/>: Der Konstruktor
/// eröffnet die Undo-Transaktion und setzt eine Vorher-Marke; <see cref="Commit"/> schließt sie
/// (mit Nachher-Marke) ab, <see cref="Cancel"/> verwirft sie, und <see cref="Dispose"/> beendet eine
/// noch offene Transaktion. So lässt sich eine Bearbeitung als einzelner Undo-Schritt bündeln.
/// </summary>
sealed class TextUndoTransaction: IDisposable {

    readonly IEditorOperations _editorOperations;        
    [CanBeNull]
    ITextUndoTransaction _transaction;
    bool _inTransaction;

    /// <summary>
    /// Eröffnet eine benannte Undo-Transaktion für die Undo-Historie von
    /// <paramref name="textView"/> und setzt die Vorher-Marke.
    /// </summary>
    /// <param name="description">Der in der Undo-Historie angezeigte Name des Schritts.</param>
    /// <param name="textView">Die Ansicht, deren Puffer bearbeitet wird.</param>
    /// <param name="undoHistoryRegistry">Registry, aus der die Undo-Historie des Puffers stammt.</param>
    /// <param name="editorOperationsFactoryService">Liefert die <see cref="IEditorOperations"/> der Ansicht.</param>
    public TextUndoTransaction(
        string description,
        ITextView textView,
        ITextUndoHistoryRegistry undoHistoryRegistry,
        IEditorOperationsFactoryService editorOperationsFactoryService) {

        _inTransaction    = true;
        _editorOperations = editorOperationsFactoryService.GetEditorOperations(textView);

        var undoHistory = undoHistoryRegistry.GetHistory(textView.TextBuffer);
        if (undoHistory != null) {
            _transaction = undoHistory.CreateTransaction(description);
            _editorOperations.AddBeforeTextBufferChangePrimitive();
        }
    }

    /// <summary>
    /// Beendet eine noch offene Transaktion (ohne sie abzuschließen) — der Aufräum-Pfad für die
    /// <c>using</c>-Verwendung, falls weder <see cref="Commit"/> noch <see cref="Cancel"/> gerufen wurde.
    /// </summary>
    public void Dispose() {
        EndTransaction();
    }
        
    /// <summary>
    /// Setzt die Nachher-Marke, schließt die Undo-Transaktion ab und beendet sie. Die gebündelte
    /// Bearbeitung wird damit als ein Undo-Schritt wirksam.
    /// </summary>
    /// <exception cref="InvalidOperationException">Die Transaktion ist bereits abgeschlossen.</exception>
    public void Commit() {
        if (!_inTransaction) {
            throw new InvalidOperationException("The transaction is already complete");
        }

        _editorOperations?.AddAfterTextBufferChangePrimitive();
        _transaction?.Complete();

        EndTransaction();
    }

    /// <summary>
    /// Verwirft die Undo-Transaktion und beendet sie; die gebündelten Änderungen werden nicht als
    /// Undo-Schritt eingetragen.
    /// </summary>
    /// <exception cref="InvalidOperationException">Die Transaktion ist bereits abgeschlossen.</exception>
    public void Cancel() {
        if (!_inTransaction) {
            throw new InvalidOperationException("The transaction is already complete");
        }

        _transaction?.Cancel();
        EndTransaction();
    }

    /// <summary>Markiert die Transaktion als abgeschlossen und gibt die zugrunde liegende
    /// <see cref="ITextUndoTransaction"/> frei.</summary>
    void EndTransaction() {
        _inTransaction = false;
        _transaction?.Dispose();
        _transaction = null;
    }
}