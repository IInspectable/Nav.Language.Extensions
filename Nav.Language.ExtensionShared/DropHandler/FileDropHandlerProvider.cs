#region Using Directives

using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.DragDrop;
using Microsoft.VisualStudio.Utilities;

using Pharmatechnik.Nav.Language.Extension.Commands;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.DropHandler; 

/// <summary>
/// MEF-Provider (<see cref="IDropHandlerProvider"/>), der für den Nav-Inhaltstyp den
/// <see cref="FileDropHandler"/> beisteuert. Über die <c>[DropFormat]</c>-Attribute meldet er sich für die
/// Formate aus <see cref="ClipBoardFormats"/> an und wird — via <c>[Order]</c> — vor dem Standard-Datei-Drop-
/// Handler eingereiht, sodass gezogene <c>.nav</c>-Dateien als Include eingefügt statt geöffnet werden.
/// </summary>
[Export(typeof(IDropHandlerProvider))]
[DropFormat(ClipBoardFormats.VsProjectItems)]
[DropFormat(ClipBoardFormats.FileDrop)]
[Name(nameof(FileDropHandlerProvider))]
[ContentType(NavLanguageContentDefinitions.ContentType)]
[Order(Before = "DefaultFileDropHandler")]
class FileDropHandlerProvider: IDropHandlerProvider {

    readonly NavEditorOperationsProvider _navEditorOperationsProvider;

    /// <summary>MEF-Konstruktor; erhält den <see cref="NavEditorOperationsProvider"/> importiert.</summary>
    [ImportingConstructor]
    public FileDropHandlerProvider(NavEditorOperationsProvider navEditorOperationsProvider) {
        _navEditorOperationsProvider = navEditorOperationsProvider;

    }

    /// <summary>
    /// Liefert den (pro Ansicht zwischengespeicherten) <see cref="FileDropHandler"/> für <paramref name="view"/>.
    /// </summary>
    public IDropHandler GetAssociatedDropHandler(IWpfTextView view) {
        return view.Properties.GetOrCreateSingletonProperty(() => new FileDropHandler(view, _navEditorOperationsProvider));
    }

}