#region Using Directives

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Immutable;

using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;

using Pharmatechnik.Nav.Language.Extension.QuickInfo;
using Pharmatechnik.Nav.Language.Text;

using Task = System.Threading.Tasks.Task;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Completion; 

class EdgeCompletionSource: AsyncCompletionSource {

    public EdgeCompletionSource(QuickinfoBuilderService quickinfoBuilderService): base(quickinfoBuilderService) {
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
               trigger.Character == '-';
    }

    public override async Task<CompletionContext> GetCompletionContextAsync(IAsyncCompletionSession session, CompletionTrigger trigger, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan, CancellationToken token) {

        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        var codeGenerationUnit = GetCodeGenerationUnit(triggerLocation);

        await Task.Yield();

        if (!ShouldProvideCompletions(triggerLocation, codeGenerationUnit, out var myApplicableToSpan) ||
            myApplicableToSpan != applicableToSpan) {
            return CreateEmptyCompletionContext();
        }

        var completionItems = ImmutableArray.CreateBuilder<CompletionItem>();

        // Edges
        foreach (var keyword in SyntaxFacts.EdgeKeywords
                                           .Where(k => !SyntaxFacts.IsHiddenKeyword(k))
                                           .OrderBy(k => k)) {

            completionItems.Add(CreateKeywordCompletion(keyword));
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

        var start       = line.GetStartOfEdge(triggerLocation);
        var triggerLine = triggerLocation.GetContainingLine();

        // Vor der Edge muss ein Whitespace sein, bzw. der Zeilenanfang
        if (start != triggerLine.Start &&
            !char.IsWhiteSpace((start - 1).GetChar())) {
            return false;
        }

        applicableToSpan = new SnapshotSpan(start, triggerLocation);

        return true;
    }

}