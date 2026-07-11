#region Using Directives

using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Threading;

using ThreadHelper = Microsoft.VisualStudio.Shell.ThreadHelper;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.QuickInfo; 

sealed class SymbolQuickInfoSource: SemanticModelServiceDependent, IAsyncQuickInfoSource {

    public SymbolQuickInfoSource(ITextBuffer textBuffer,
                                 QuickinfoBuilderService quickinfoBuilderService): base(textBuffer) {

        QuickinfoBuilderService = quickinfoBuilderService;
    }

    public QuickinfoBuilderService QuickinfoBuilderService { get; }

    public async Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken) {

        await Task.Yield().ConfigureAwait(false);
        if (cancellationToken.IsCancellationRequested) {
            return null;
        }

        var codeGenerationUnitAndSnapshot = SemanticModelService.CodeGenerationUnitAndSnapshot;
        if (codeGenerationUnitAndSnapshot == null) {
            return null;
        }

        // Map the trigger point down to our buffer.
        SnapshotPoint? subjectTriggerPoint = session.GetTriggerPoint(codeGenerationUnitAndSnapshot.Snapshot);
        if (!subjectTriggerPoint.HasValue) {
            return null;
        }

        var position      = subjectTriggerPoint.Value.Position;
        var triggerSymbol = codeGenerationUnitAndSnapshot.CodeGenerationUnit.Symbols.FindAtPosition(position);

        // Kein Symbol unter dem Caret — steht dort ein Keyword-Token, zeigt der Tooltip dessen Bedeutung
        // (SyntaxFacts, die einzige Autorität). Spiegelt den Keyword-Fallback des LSP-Hovers (NavHoverService).
        if (triggerSymbol == null) {
            return await BuildKeywordQuickInfoAsync(codeGenerationUnitAndSnapshot, position);
        }

        var location = triggerSymbol.Location;
        var applicableToSpan = codeGenerationUnitAndSnapshot.Snapshot.CreateTrackingSpan(
            location.Start,
            location.Length,
            SpanTrackingMode.EdgeExclusive);

        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        var qiContent = QuickinfoBuilderService.BuildSymbolQuickInfoContent(triggerSymbol);
        if (qiContent == null) {
            return null;
        }

        return new QuickInfoItem(applicableToSpan: applicableToSpan,
                                 item: qiContent);
    }

    async Task<QuickInfoItem> BuildKeywordQuickInfoAsync(CodeGenerationUnitAndSnapshot codeGenerationUnitAndSnapshot, int position) {

        var token = codeGenerationUnitAndSnapshot.CodeGenerationUnit.Syntax.SyntaxTree.Tokens.FindAtPosition(position);
        if (token.IsMissing || !SyntaxFacts.IsKeywordClassification(token.Classification)) {
            return null;
        }

        var keyword     = token.ToString();
        var description = SyntaxFacts.GetKeywordDescription(token);
        if (description.Length == 0) {
            return null;
        }

        var applicableToSpan = codeGenerationUnitAndSnapshot.Snapshot.CreateTrackingSpan(
            token.Start,
            token.Length,
            SpanTrackingMode.EdgeExclusive);

        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        return new QuickInfoItem(applicableToSpan: applicableToSpan,
                                 item: QuickinfoBuilderService.BuildKeywordQuickInfoContent(keyword, description));
    }

}