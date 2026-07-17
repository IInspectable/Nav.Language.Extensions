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

/// <summary>
/// Command-Handler für „Go To Definition" (F12) in Nav-Dateien. Bestimmt über einen
/// <see cref="GoToTag"/>-Tag-Aggregator das Sprungziel unter dem Cursor und navigiert mittels des
/// <see cref="GoToLocationService"/> dorthin — bevorzugt in einen Vorschau-Tab. Liegt kein navigierbares
/// Symbol unter dem Cursor, wird eine Hinweismeldung angezeigt.
/// </summary>
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

    /// <summary>Im Command-System angezeigter Name des Befehls.</summary>
    public string DisplayName => "Go To Definition";

    /// <summary>Verfügbar nur für einen <see cref="IWpfTextView"/>, sonst <see cref="CommandState.Unavailable"/>.</summary>
    public CommandState GetCommandState(GoToDefinitionCommandArgs args) {
        return args.TextView is IWpfTextView ? CommandState.Available : CommandState.Unavailable;
    }

    /// <summary>
    /// Navigiert asynchron zum <see cref="GoToTag"/> unter dem Cursor: aggregiert die Sprungziel-Tags,
    /// wechselt auf den UI-Thread und öffnet über <see cref="GoToLocationService.GoToLocationInPreviewTabAsync"/>
    /// das Ziel im Vorschau-Tab; ohne Ziel wird eine Info-Meldung ausgegeben.
    /// </summary>
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