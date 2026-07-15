#region Using Directives

using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Threading;

using ThreadHelper = Microsoft.VisualStudio.Shell.ThreadHelper;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.QuickInfo; 

/// <summary>
/// Die eigentliche Nav-Hover-Quelle: zeigt zum Symbol unter dem Cursor dessen Signatur und Beschreibung an,
/// bzw. — steht dort ein Schlüsselwort — dessen Bedeutung (Spiegel des LSP-Hover-Verhaltens). Erfüllt den
/// VS-SDK-Vertrag <see cref="IAsyncQuickInfoSource"/>, bezieht das Semantikmodell über den
/// <see cref="SemanticModelServiceDependent"/>-Basispfad und rendert den Inhalt mit dem
/// <see cref="QuickinfoBuilderService"/>.
/// </summary>
sealed class SymbolQuickInfoSource: SemanticModelServiceDependent, IAsyncQuickInfoSource {

    public SymbolQuickInfoSource(ITextBuffer textBuffer,
                                 QuickinfoBuilderService quickinfoBuilderService): base(textBuffer) {

        QuickinfoBuilderService = quickinfoBuilderService;
    }

    /// <summary>Der Dienst, der den QuickInfo-Inhalt (Symbol/Keyword) als WPF-Element aufbaut.</summary>
    public QuickinfoBuilderService QuickinfoBuilderService { get; }

    /// <summary>
    /// VS-SDK-Vertrag: baut den Hover-Inhalt zum <see cref="ISymbol"/> unter dem Trigger-Punkt. Findet dort
    /// kein Symbol, greift der Keyword-Fallback (<see cref="BuildKeywordQuickInfoAsync"/>). Liefert
    /// <c>null</c>, wenn weder Symbol noch dokumentiertes Keyword vorliegt.
    /// </summary>
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

    /// <summary>
    /// Keyword-Fallback des Hovers: steht an <paramref name="position"/> ein Schlüsselwort-Token mit
    /// hinterlegter Beschreibung (<see cref="SyntaxFacts.GetKeywordDescription(SyntaxToken)"/>), wird dessen Bedeutung
    /// als QuickInfo gezeigt; sonst <c>null</c>.
    /// </summary>
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