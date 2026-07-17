#region Using Directives

using System.Windows;
using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Utilities;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Commands; 

/// <summary>
/// Command-Handler für „Paste" (Strg+V) im Nav-Editor. Fängt das Einfügen ab, wenn die Zwischenablage auf
/// eine <c>.nav</c>-Datei verweist, und fügt an der Cursor-Position ein passendes
/// <see cref="SyntaxFacts.TaskrefKeyword"/>-Statement ein (siehe <see cref="PasteNavFileCommand"/>);
/// andernfalls bleibt der Befehl unbehandelt und die Standard-Einfügeoperation greift.
/// </summary>
[Export(typeof(ICommandHandler))]
[ContentType(NavLanguageContentDefinitions.ContentType)]
[TextViewRole(PredefinedTextViewRoles.Editable)]
[Name(CommandHandlerNames.PasteCommandHandler)]
class PasteCommandHandler: ICommandHandler<PasteCommandArgs> {

    readonly NavEditorOperationsProvider _navEditorOperationsProvider;

    [ImportingConstructor]
    public PasteCommandHandler(NavEditorOperationsProvider navEditorOperationsProvider) {
        _navEditorOperationsProvider = navEditorOperationsProvider;

    }

    /// <summary>Im Command-System angezeigter Name des Befehls.</summary>
    public string DisplayName => "Paste";

    /// <summary>
    /// Versucht, den Zwischenablage-Inhalt als taskref-Verweis einzufügen; liefert das Ergebnis von
    /// <see cref="PasteNavFileCommand.Execute"/> (<see langword="false"/> = nicht behandelt, Standard-Paste greift).
    /// </summary>
    public bool ExecuteCommand(PasteCommandArgs args, CommandExecutionContext executionContext) {
            
        ThreadHelper.ThrowIfNotOnUIThread();

        var pasteCommand = GetPasteNavFileCommand(args);
        return pasteCommand.Execute(Clipboard.GetDataObject());

    }

    /// <summary>Verfügbar, wenn die Zwischenablage auf eine <c>.nav</c>-Datei verweist, sonst <see cref="CommandState.Unavailable"/>.</summary>
    public CommandState GetCommandState(PasteCommandArgs args) {

        var pasteCommand = GetPasteNavFileCommand(args);
        return pasteCommand.CanExecute(Clipboard.GetDataObject()) ? CommandState.Available : CommandState.Unavailable;
    }

    /// <summary>Erzeugt über den <see cref="NavEditorOperationsProvider"/> den <see cref="PasteNavFileCommand"/> für die View der <paramref name="args"/>.</summary>
    PasteNavFileCommand GetPasteNavFileCommand(PasteCommandArgs args) {

        var pasteCommand = _navEditorOperationsProvider.CreatePasteNavFileCommand(args.TextView);
        return pasteCommand;
    }

}