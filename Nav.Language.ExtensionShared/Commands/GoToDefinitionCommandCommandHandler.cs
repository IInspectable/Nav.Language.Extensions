#region Using Directives

using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

using Pharmatechnik.Nav.Language.Extension.Common;
using Pharmatechnik.Nav.Language.Extension.GoToLocation;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Commands; 

[Export(typeof(ICommandHandler))]
[ContentType(NavLanguageContentDefinitions.ContentType)]
[Name(CommandHandlerNames.GoToDefinitionCommandCommandHandler)]
class GoToDefinitionCommandCommandHandler: ICommandHandler<GoToDefinitionCommandArgs> {

    readonly GoToLocationService              _goToLocationService;
    readonly IViewTagAggregatorFactoryService _viewTagAggregatorFactoryService;

    [ImportingConstructor]
    public GoToDefinitionCommandCommandHandler(IViewTagAggregatorFactoryService viewTagAggregatorFactoryService, GoToLocationService goToLocationService) {
        _goToLocationService             = goToLocationService;
        _viewTagAggregatorFactoryService = viewTagAggregatorFactoryService;
    }

    public string DisplayName => "Go To Definition";

    public CommandState GetCommandState(GoToDefinitionCommandArgs args) {
        return args.TextView is IWpfTextView ? CommandState.Available : CommandState.Unavailable;
    }

    public bool ExecuteCommand(GoToDefinitionCommandArgs args, CommandExecutionContext executionContext) {

        NavLanguagePackage.Jtf.RunAsync(async () => {

            using var tagAggregator = _viewTagAggregatorFactoryService.CreateTagAggregator<GoToTag>(args.TextView);
            var       textView      = args.TextView as IWpfTextView;

            if (textView == null) {
                return;
            }

            var navigateToTagSpan = textView.GetGoToDefinitionTagSpanAtCaretPosition(tagAggregator);

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (navigateToTagSpan == null) {
                ShellUtil.ShowInfoMessage("Cannot navigate to the symbol under the caret.");
                return;
            }

            var placementRectangle = textView.TextViewLines.GetTextMarkerGeometry(navigateToTagSpan.Span).Bounds;
            placementRectangle.Offset(-args.TextView.ViewportLeft, -args.TextView.ViewportTop);

            await _goToLocationService.GoToLocationInPreviewTabAsync(
                originatingTextView: textView,
                placementRectangle : placementRectangle,
                provider           : navigateToTagSpan.Tag.Provider);
        }).FileAndForget("nav/gotodefinition/execute");

        return true;
    }

}