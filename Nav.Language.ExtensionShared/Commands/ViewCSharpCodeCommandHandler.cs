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
/// <summary>
/// Command-Handler für „View Code" (F7) in Nav-Dateien — der Sprung von einer Task/Task-Deklaration zum
/// zugehörigen generierten C#-Code. Anders als die übrigen Handler folgt er dem hauseigenen
/// <see cref="INavCommandHandler{T}"/>-Vertrag (registriert über <see cref="ExportCommandHandlerAttribute"/>).
/// Er baut zu jeder Task/Task-Deklaration einen <see cref="GoToTag"/> mit dem passenden
/// <see cref="ILocationInfoProvider"/> (IBegin-Interface bzw. Task-Deklaration), wählt den Tag an der
/// Cursor-Position und navigiert über den <see cref="GoToLocationService"/> in den Vorschau-Tab. Findet sich
/// nichts, wird an die Standard-Behandlung weitergereicht.
/// </summary>
[ExportCommandHandler(CommandHandlerNames.ViewCSharpCodeCommandHandler, NavLanguageContentDefinitions.ContentType)]
class ViewCSharpCodeCommandHandler: INavCommandHandler<ViewCodeCommandArgs> {

    readonly GoToLocationService _goToLocationService;

    [ImportingConstructor]
    public ViewCSharpCodeCommandHandler(GoToLocationService goToLocationService) {
        _goToLocationService = goToLocationService;
    }

    /// <summary>Der Befehl ist stets verfügbar.</summary>
    public CommandState GetCommandState(ViewCodeCommandArgs args, Func<CommandState> nextHandler) {
        return CommandState.Available;
    }

    /// <summary>
    /// Navigiert asynchron zum generierten C#-Code der Task unter dem Cursor. Ohne aktuelles Semantikmodell
    /// oder passenden <see cref="GoToTag"/> wird über <paramref name="nextHandler"/> an die
    /// Standard-Behandlung weitergereicht.
    /// </summary>
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
        }).FileAndForget("nav/viewcsharpcode/execute");
    }

    /// <summary>
    /// Wählt aus allen aufgebauten Sprungziel-Tags denjenigen an der Cursor-Position; liegt der Cursor in
    /// keinem Tag, wird der erste dahinterliegende bzw. hilfsweise der letzte Tag verwendet.
    /// </summary>
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

    /// <summary>
    /// Erzeugt je Task-Deklaration (Sprung zum generierten IBegin-Interface) und je Task-Definition (Sprung
    /// zur generierten Task-Deklaration) einen <see cref="GoToTag"/> mit dem passenden
    /// <see cref="ILocationInfoProvider"/>, verankert an der Location im Nav-Quelltext.
    /// </summary>
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

    /// <summary>Bildet aus einer Nav-<see cref="Location"/> und einem <paramref name="provider"/> einen <see cref="GoToTag"/>-<see cref="TagSpan{T}"/> im aktuellen Snapshot.</summary>
    TagSpan<GoToTag> CreateTagSpan(CodeGenerationUnitAndSnapshot codeGenerationUnitAndSnapshot, Location sourceLocation, ILocationInfoProvider provider) {
        var tagSpan = new SnapshotSpan(codeGenerationUnitAndSnapshot.Snapshot, sourceLocation.Start, sourceLocation.Length);
        var tag     = new GoToTag(provider);

        return new TagSpan<GoToTag>(tagSpan, tag);
    }

}