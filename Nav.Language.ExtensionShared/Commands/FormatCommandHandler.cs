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

    public string DisplayName => "Format Nav Document";

    public CommandState GetCommandState(FormatDocumentCommandArgs args) {
        return CommandState.Available;
    }

    public bool ExecuteCommand(FormatDocumentCommandArgs args, CommandExecutionContext executionContext) {
        ThreadHelper.ThrowIfNotOnUIThread();
        NavFormatCommand.FormatDocument(args.TextView, _textChangeService);
        return true;
    }

    public CommandState GetCommandState(FormatSelectionCommandArgs args) {
        return CommandState.Available;
    }

    public bool ExecuteCommand(FormatSelectionCommandArgs args, CommandExecutionContext executionContext) {
        ThreadHelper.ThrowIfNotOnUIThread();
        NavFormatCommand.FormatSelection(args.TextView, _textChangeService);
        return true;
    }

}
