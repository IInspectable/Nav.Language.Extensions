#region Using Directives

using System.Threading;
using System.Collections.Generic;
using System.Collections.Immutable;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;

using Pharmatechnik.Nav.Language.Completion;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Completion;

class CompletionCommitManager: IAsyncCompletionCommitManager {

    // Abschluss-Zeichen stammen aus der einen Autorität NavCompletionService.CommitCharacters
    // (VS + LSP konsistent).
    readonly ImmutableArray<char> _commitChars = NavCompletionService.CommitCharacters.ToImmutableArray();

    public bool ShouldCommitCompletion(IAsyncCompletionSession session, SnapshotPoint location, char typedChar, CancellationToken token) {
        return true;
    }

    public CommitResult TryCommit(IAsyncCompletionSession session, ITextBuffer buffer, CompletionItem item, char typedChar, CancellationToken token) {
        if (item.Properties.TryGetProperty<ITrackingSpan>(AsyncCompletionSource.ReplacementTrackingSpanProperty, out var replacementSpan)) {

            using var edit = buffer.CreateEdit();

            edit.Replace(replacementSpan.GetSpan(buffer.CurrentSnapshot), item.InsertText);
            edit.Apply();

            return CommitResult.Handled;

        }

        return CommitResult.Unhandled; // use default commit mechanism.
    }

    public IEnumerable<char> PotentialCommitCharacters => _commitChars;

}