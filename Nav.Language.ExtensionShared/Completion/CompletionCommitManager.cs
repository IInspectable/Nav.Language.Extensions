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

/// <summary>
/// Der VS-Host-Commit-Manager der IntelliSense-Completion. Erfüllt den VS-SDK-Vertrag
/// <see cref="IAsyncCompletionCommitManager"/>: er meldet die Abschluss-Zeichen (aus der einen Autorität
/// <see cref="NavCompletionService.CommitCharacters"/>) und führt den Commit aus. Trägt das Item einen
/// eigenen Ersetzungsbereich (<see cref="AsyncCompletionSource.ReplacementTrackingSpanProperty"/>, etwa
/// bei Pfaden oder Edge-Keywords), ersetzt er genau diesen — sonst greift der Standard-Mechanismus von VS.
/// </summary>
class CompletionCommitManager: IAsyncCompletionCommitManager {

    // Abschluss-Zeichen stammen aus der einen Autorität NavCompletionService.CommitCharacters
    // (VS + LSP konsistent).
    readonly ImmutableArray<char> _commitChars = NavCompletionService.CommitCharacters.ToImmutableArray();

    /// <summary>
    /// VS-SDK-Vertrag: ob das getippte Zeichen an dieser Stelle einen Commit auslösen darf. Nav lässt
    /// jedes gemeldete Commit-Zeichen zu (liefert stets <c>true</c>).
    /// </summary>
    public bool ShouldCommitCompletion(IAsyncCompletionSession session, SnapshotPoint location, char typedChar, CancellationToken token) {
        return true;
    }

    /// <summary>
    /// VS-SDK-Vertrag: übernimmt das ausgewählte Item. Trägt es einen per-Item-Ersetzungsbereich
    /// (<see cref="AsyncCompletionSource.ReplacementTrackingSpanProperty"/>), wird dieser Bereich durch
    /// den Einfügetext ersetzt (<see cref="CommitResult.Handled"/>); andernfalls
    /// <see cref="CommitResult.Unhandled"/>, damit VS den Standard-Commit über den Identifier-Span fährt.
    /// </summary>
    public CommitResult TryCommit(IAsyncCompletionSession session, ITextBuffer buffer, CompletionItem item, char typedChar, CancellationToken token) {
        if (item.Properties.TryGetProperty<ITrackingSpan>(AsyncCompletionSource.ReplacementTrackingSpanProperty, out var replacementSpan)) {

            using var edit = buffer.CreateEdit();

            edit.Replace(replacementSpan.GetSpan(buffer.CurrentSnapshot), item.InsertText);
            edit.Apply();

            return CommitResult.Handled;

        }

        return CommitResult.Unhandled; // use default commit mechanism.
    }

    /// <summary>VS-SDK-Vertrag: die Zeichen, die potenziell einen Commit auslösen (aus <see cref="NavCompletionService.CommitCharacters"/>).</summary>
    public IEnumerable<char> PotentialCommitCharacters => _commitChars;

}