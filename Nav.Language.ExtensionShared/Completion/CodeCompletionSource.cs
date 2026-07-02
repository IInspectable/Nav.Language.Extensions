#region Using Directives

using System.Threading;
using System.Threading.Tasks;
using System.Collections.Immutable;
using System.Linq;

using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;

using Pharmatechnik.Nav.Language.Extension.QuickInfo;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Completion; 

class CodeCompletionSource: AsyncCompletionSource {

    public CodeCompletionSource(QuickinfoBuilderService quickinfoBuilderService)
        : base(quickinfoBuilderService) {

    }

    public override CompletionStartData InitializeCompletion(CompletionTrigger trigger, SnapshotPoint triggerLocation, CancellationToken token) {

        if (!ShouldTriggerCompletion(trigger)) {
            return CompletionStartData.DoesNotParticipateInCompletion;
        }

        if (ShouldProvideCompletions(triggerLocation, out var applicableToSpan)) {
            return new CompletionStartData(CompletionParticipation.ProvidesItems, applicableToSpan);
        }

        return CompletionStartData.DoesNotParticipateInCompletion;

    }

    public override Task<CompletionContext> GetCompletionContextAsync(IAsyncCompletionSession session, CompletionTrigger trigger, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan, CancellationToken token) {

        if (ShouldProvideCompletions(triggerLocation, out _)) {
            var completionItems = ImmutableArray.CreateBuilder<CompletionItem>();

            foreach (var keyword in SyntaxFacts.CodeKeywords
                                               .Where(k => !SyntaxFacts.IsHiddenKeyword(k))
                                               .OrderBy(k => k)) {

                completionItems.Add(CreateKeywordCompletion(keyword));

            }

            return CreateCompletionContextTaskAsync(completionItems);
        }

        return CreateEmptyCompletionContextTaskAsync();
    }

    bool ShouldProvideCompletions(SnapshotPoint triggerLocation, out SnapshotSpan applicableToSpan) {

        var line                       = triggerLocation.GetContainingLine();
        var start                      = line.GetStartOfIdentifier(triggerLocation);
        var previousNonWhitespacePoint = line.GetPreviousNonWhitespace(start);
        var previousNonWhitespace      = previousNonWhitespacePoint?.GetChar();

        applicableToSpan = default;

        if (previousNonWhitespace == SyntaxFacts.OpenBracket) {
            applicableToSpan = new SnapshotSpan(start, triggerLocation);
            return true;
        }

        return false;
    }

}