#region Using Directives

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;

using Pharmatechnik.Nav.Language.Completion;
using Pharmatechnik.Nav.Language.Extension.QuickInfo;
using Pharmatechnik.Nav.Language.Text;

using Task = System.Threading.Tasks.Task;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Completion; 

// ReSharper disable once InconsistentNaming
class NavCompletionSource: AsyncCompletionSource {

    public NavCompletionSource(QuickinfoBuilderService quickinfoBuilderService): base(quickinfoBuilderService) {

    }

    public override CompletionStartData InitializeCompletion(CompletionTrigger trigger, SnapshotPoint triggerLocation, CancellationToken token) {

        ThreadHelper.ThrowIfNotOnUIThread();

        if (!ShouldTriggerCompletion(trigger)) {
            return CompletionStartData.DoesNotParticipateInCompletion;
        }

        var codeGenerationUnit = GetCodeGenerationUnit(triggerLocation);

        if (ShouldProvideCompletions(triggerLocation, codeGenerationUnit, out var applicableToSpan)) {
            return new CompletionStartData(CompletionParticipation.ProvidesItems, applicableToSpan);
        }

        return CompletionStartData.DoesNotParticipateInCompletion;
    }

    protected override bool ShouldTriggerCompletionOverride(CompletionTrigger trigger) {
        return char.IsLetter(trigger.Character) ||
               trigger.Character == SyntaxFacts.Colon;
    }

    public override async Task<CompletionContext> GetCompletionContextAsync(IAsyncCompletionSession session, CompletionTrigger trigger, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan, CancellationToken token) {

        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        var codeGenerationUnit = GetCodeGenerationUnit(triggerLocation);

        if (!ShouldProvideCompletions(triggerLocation, codeGenerationUnit, out var myApplicableToSpan) ||
            myApplicableToSpan != applicableToSpan) {
            return CreateEmptyCompletionContext();
        }

        await Task.Yield();

        var completionItems = ImmutableArray.CreateBuilder<CompletionItem>();

        // Eine Quelle der Wahrheit: der Engine-Service entscheidet kontextsensitiv über den Syntaxbaum, was
        // an dieser Position sinnvoll ist. Diese Quelle zeigt alles AUSSER den Edge-Keywords — die liefert
        // EdgeCompletionSource mit ihrem eigenen Ersetzungsbereich.
        foreach (var item in NavCompletionService.GetCompletions(codeGenerationUnit, triggerLocation)) {

            if (IsEdgeKeyword(item)) {
                continue;
            }

            completionItems.Add(ToCompletionItem(item));
        }

        return CreateCompletionContext(completionItems);
    }

    bool ShouldProvideCompletions(SnapshotPoint triggerLocation, CodeGenerationUnit codeGenerationUnit, out SnapshotSpan applicableToSpan) {

        applicableToSpan = default;

        // Keine Autocompletion in Kommentaren! — aus der angehängten Trivia (Roslyn-Modell), nicht über ein
        // FindToken auf den flachen Strom.
        if (codeGenerationUnit.Syntax.SyntaxTree.IsPositionInComment(triggerLocation)) {
            return false;
        }

        // Kein Auto Completion in ""
        var line         = triggerLocation.GetContainingLine();
        var linePosition = triggerLocation - line.Start;
        var lineText     = line.GetText();

        if (lineText.IsInQuotation(linePosition)) {
            return false;
        }

        // Kein Auto Completion in Code Blöcken
        // TODO Nicht vollständig, da nur aktuelle Zeile betrachtet wird
        var isInCodeBlock = lineText.IsInTextBlock(linePosition, SyntaxFacts.OpenBracket, SyntaxFacts.CloseBracket);
        if (isInCodeBlock) {
            return false;
        }

        var start = line.GetStartOfIdentifier(triggerLocation);

        applicableToSpan = new SnapshotSpan(start, triggerLocation);

        return true;
    }

}