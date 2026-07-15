#region Using Directives

using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Utilities;

using Pharmatechnik.Nav.Language.Extension.Common;
using Pharmatechnik.Nav.Language.Extension.CodeFixes;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Commands;

/// <summary>
/// Command-Handler für die Standard-Editorbefehle „Format Document" (Ctrl+E, D) und „Format Selection"
/// (Ctrl+K, Ctrl+F) in Nav-Dateien. Delegiert an den gemeinsamen Einstiegspunkt <see cref="NavFormatCommand"/>,
/// der das rein syntaktische Formatieren über den Engine-Kern <c>NavFormattingService</c> ausführt und die
/// resultierenden Änderungen undo-fähig über den <see cref="ITextChangeService"/> anwendet.
/// </summary>
[Export(typeof(ICommandHandler))]
[ContentType(NavLanguageContentDefinitions.ContentType)]
[Name(CommandHandlerNames.FormatCommandHandler)]
class FormatCommandHandler: ICommandHandler<FormatDocumentCommandArgs>,
                            ICommandHandler<FormatSelectionCommandArgs> {

    readonly ITextChangeService _textChangeService;

    [ImportingConstructor]
    public FormatCommandHandler(ITextChangeService textChangeService) {
        _textChangeService = textChangeService;
    }

    /// <summary>Im Command-System angezeigter Name des Befehls.</summary>
    public string DisplayName => "Format Nav Document";

    /// <summary>„Format Document" ist stets verfügbar.</summary>
    public CommandState GetCommandState(FormatDocumentCommandArgs args) {
        return CommandState.Available;
    }

    /// <summary>Formatiert das gesamte Dokument über <see cref="NavFormatCommand.FormatDocument"/>.</summary>
    public bool ExecuteCommand(FormatDocumentCommandArgs args, CommandExecutionContext executionContext) {
        ThreadHelper.ThrowIfNotOnUIThread();
        NavFormatCommand.FormatDocument(args.TextView, _textChangeService);
        return true;
    }

    /// <summary>„Format Selection" ist stets verfügbar.</summary>
    public CommandState GetCommandState(FormatSelectionCommandArgs args) {
        return CommandState.Available;
    }

    /// <summary>Formatiert nur die aktuelle Selektion über <see cref="NavFormatCommand.FormatSelection"/>.</summary>
    public bool ExecuteCommand(FormatSelectionCommandArgs args, CommandExecutionContext executionContext) {
        ThreadHelper.ThrowIfNotOnUIThread();
        NavFormatCommand.FormatSelection(args.TextView, _textChangeService);
        return true;
    }

}
