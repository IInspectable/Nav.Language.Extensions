#region Using Directives

using System;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

using Pharmatechnik.Nav.Language.CodeGen;
using Pharmatechnik.Nav.Language.Extension.Common;
using Pharmatechnik.Nav.Language.Extension.GoToLocation;
using Pharmatechnik.Nav.Language.Extension.GoToLocation.Provider;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Commands; 

// TODO Code Review
[ExportCommandHandler(CommandHandlerNames.ViewCSharpCodeCommandHandler, NavLanguageContentDefinitions.ContentType)]
class ViewCSharpCodeCommandHandler: INavCommandHandler<ViewCodeCommandArgs> {

    readonly GoToLocationService _goToLocationService;

    [ImportingConstructor]
    public ViewCSharpCodeCommandHandler(GoToLocationService goToLocationService) {
        _goToLocationService = goToLocationService;
    }

    public CommandState GetCommandState(ViewCodeCommandArgs args, Func<CommandState> nextHandler) {
        return CommandState.Available;
    }

    public void ExecuteCommand(ViewCodeCommandArgs args, Action nextHandler) {

        NavLanguagePackage.Jtf.RunAsync(async () => {

            var semanticModelService          = SemanticModelService.TryGet(args.SubjectBuffer);
            var codeGenerationUnitAndSnapshot = semanticModelService?.CodeGenerationUnitAndSnapshot;
            if (codeGenerationUnitAndSnapshot == null) {
                nextHandler();
                return;
            }

            var navigateToTagSpan = GetGoToCodeTagSpanAtCaretPosition(codeGenerationUnitAndSnapshot, args);
            if (navigateToTagSpan == null) {
                nextHandler();
                return;
            }

            var caretSpan     = args.TextView.Caret.Position.BufferPosition.ExtendToLength1();
            var caretGeometry = args.TextView.TextViewLines.GetTextMarkerGeometry(caretSpan);
            if (caretGeometry == null) {
                nextHandler();
                return;
            }

            var placementRectangle = caretGeometry.Bounds;
            placementRectangle.Offset(-args.TextView.ViewportLeft, -args.TextView.ViewportTop);

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            await _goToLocationService.GoToLocationInPreviewTabAsync(
                originatingTextView: args.TextView,
                placementRectangle: placementRectangle,
                provider: navigateToTagSpan.Tag.Provider);
        });
    }

    TagSpan<GoToTag> GetGoToCodeTagSpanAtCaretPosition(CodeGenerationUnitAndSnapshot codeGenerationUnitAndSnapshot, ViewCodeCommandArgs args) {

        var tags = BuildTagSpans(codeGenerationUnitAndSnapshot, args.SubjectBuffer)
                  .OrderBy(tag => tag.Span.Start.Position)
                  .ToList();

        var caretPosition     = args.TextView.Caret.Position.BufferPosition;
        var navigateToTagSpan = tags.FirstOrDefault(tagSpan => caretPosition >= tagSpan.Span.Start.Position && caretPosition <= tagSpan.Span.End.Position);

        if (navigateToTagSpan != null) {
            return navigateToTagSpan;
        }

        navigateToTagSpan = tags.FirstOrDefault(tagSpan => caretPosition < tagSpan.Span.Start.Position && caretPosition < tagSpan.Span.End.Position)
                         ?? tags.LastOrDefault(); // Den letzten Eintrag wählen

        return navigateToTagSpan;
    }

    IEnumerable<TagSpan<GoToTag>> BuildTagSpans(CodeGenerationUnitAndSnapshot codeGenerationUnitAndSnapshot, ITextBuffer subjectBuffer) {

        foreach (var taskDeclaration in codeGenerationUnitAndSnapshot.CodeGenerationUnit.TaskDeclarations.Where(td => !td.IsIncluded && td.Origin == TaskDeclarationOrigin.TaskDeclaration)) {
            var codeModel = TaskDeclarationCodeInfo.FromTaskDeclaration(taskDeclaration);
            var provider  = new TaskIBeginInterfaceDeclarationCodeFileLocationInfoProvider(subjectBuffer, codeModel);

            yield return CreateTagSpan(codeGenerationUnitAndSnapshot, taskDeclaration.Syntax?.GetLocation(), provider);
        }

        foreach (var taskDefinition in codeGenerationUnitAndSnapshot.CodeGenerationUnit.TaskDefinitions) {
            var codeModel = TaskCodeInfo.FromTaskDefinition(taskDefinition);
            var provider  = new TaskDeclarationCodeFileLocationInfoProvider(subjectBuffer, codeModel);

            yield return CreateTagSpan(codeGenerationUnitAndSnapshot, taskDefinition.Syntax.GetLocation(), provider);
        }
    }

    TagSpan<GoToTag> CreateTagSpan(CodeGenerationUnitAndSnapshot codeGenerationUnitAndSnapshot, Location sourceLocation, ILocationInfoProvider provider) {
        var tagSpan = new SnapshotSpan(codeGenerationUnitAndSnapshot.Snapshot, sourceLocation.Start, sourceLocation.Length);
        var tag     = new GoToTag(provider);

        return new TagSpan<GoToTag>(tagSpan, tag);
    }

}