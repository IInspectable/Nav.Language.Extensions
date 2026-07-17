#region Using Directives

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.DragDrop;

using Pharmatechnik.Nav.Language.Extension.Commands;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.DropHandler; 

/// <summary>
/// <see cref="IDropHandler"/> für den Nav-Editor, der auf die Ansicht gezogene <c>.nav</c>-Dateien behandelt:
/// Er setzt das Ziehen als Einfügen des Datei-Include an der Caret-Position um, indem er den
/// <c>PasteNavFileCommand</c> aus dem <see cref="NavEditorOperationsProvider"/> ausführt. Instanziiert vom
/// <see cref="FileDropHandlerProvider"/>.
/// </summary>
class FileDropHandler: IDropHandler {

    readonly IWpfTextView                _textView;
    readonly NavEditorOperationsProvider _navEditorOperationsProvider;

    /// <summary>Erzeugt den Drop-Handler für die angegebene Editor-Ansicht.</summary>
    public FileDropHandler(IWpfTextView textView, NavEditorOperationsProvider navEditorOperationsProvider) {
        _textView                    = textView;
        _navEditorOperationsProvider = navEditorOperationsProvider;
    }

    /// <summary>
    /// Führt das Fallenlassen aus: fügt die gezogene Datei über den <c>PasteNavFileCommand</c> ein. Liefert
    /// <see cref="DragDropPointerEffects.Link"/> bei Erfolg, sonst <see cref="DragDropPointerEffects.None"/>.
    /// </summary>
    public DragDropPointerEffects HandleDataDropped(DragDropInfo dragDropInfo) {

        ThreadHelper.ThrowIfNotOnUIThread();

        var pasteCommand = GetPasteNavFileCommand();

        var executed = pasteCommand.Execute(dragDropInfo.Data);
        return executed ? DragDropPointerEffects.Link : DragDropPointerEffects.None;

    }

    /// <summary>Wird beim Abbruch des Ziehvorgangs aufgerufen; hier ohne weitere Aktion.</summary>
    public void HandleDragCanceled() {
    }

    /// <summary>Meldet beim Start des Ziehvorgangs den Verknüpfungs-Effekt (<see cref="DragDropPointerEffects.Link"/>).</summary>
    public DragDropPointerEffects HandleDragStarted(DragDropInfo dragDropInfo) {
        return DragDropPointerEffects.Link;
    }

    /// <summary>
    /// Führt den Cursor während des Ziehens an die Position unter dem Mauszeiger nach und meldet den
    /// Verknüpfungs-Effekt.
    /// </summary>
    public DragDropPointerEffects HandleDraggingOver(DragDropInfo dragDropInfo) {

        _textView.Caret.MoveTo(dragDropInfo.VirtualBufferPosition);

        return DragDropPointerEffects.Link;
    }

    /// <summary>
    /// Gibt an, ob die gezogenen Daten hier fallen gelassen werden dürfen — geprüft über
    /// <c>PasteNavFileCommand.CanExecute</c>.
    /// </summary>
    public bool IsDropEnabled(DragDropInfo dragDropInfo) {

        var pasteCommand = GetPasteNavFileCommand();

        return pasteCommand.CanExecute(dragDropInfo.Data);
    }

    /// <summary>Erzeugt den <c>PasteNavFileCommand</c> für die aktuelle Editor-Ansicht.</summary>
    PasteNavFileCommand GetPasteNavFileCommand() {

        var pasteCommand = _navEditorOperationsProvider.CreatePasteNavFileCommand(_textView);
        return pasteCommand;
    }

}