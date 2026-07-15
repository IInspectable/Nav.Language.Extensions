#region Using Directives

using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Commands; 

/// <summary>
/// MEF-Fabrik für <see cref="PasteNavFileCommand"/>. Kapselt den benötigten
/// <see cref="IEditorOperationsFactoryService"/>, damit der <see cref="PasteCommandHandler"/> pro
/// <see cref="ITextView"/> einen Einfügebefehl erzeugen kann, ohne den VS-Dienst selbst zu importieren.
/// </summary>
[Export(typeof(NavEditorOperationsProvider))]
class NavEditorOperationsProvider {

    readonly IEditorOperationsFactoryService _editorOperationsFactoryService;

    [ImportingConstructor]
    public NavEditorOperationsProvider(IEditorOperationsFactoryService editorOperationsFactoryService) {
        _editorOperationsFactoryService = editorOperationsFactoryService;

    }

    /// <summary>Erzeugt einen <see cref="PasteNavFileCommand"/> für die angegebene <paramref name="textView"/>.</summary>
    public PasteNavFileCommand CreatePasteNavFileCommand(ITextView textView) {
        return new PasteNavFileCommand(textView, _editorOperationsFactoryService);
    }

}