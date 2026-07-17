#region Using Directives

using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Threading;

using ThreadHelper = Microsoft.VisualStudio.Shell.ThreadHelper;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.QuickInfo; 

/// <summary>
/// Diagnose-Hover für Sprachentwickler: zeigt bei gedrückt gehaltenem <c>Strg+Umschalt</c> zum Token unter
/// dem Cursor dessen Art, Klassifikation, Position und Parent-Syntaxknoten. Erfüllt den VS-SDK-Vertrag
/// <see cref="IAsyncQuickInfoSource"/> und bezieht den Syntaxbaum über den <see cref="ParserServiceDependent"/>-Basispfad.
/// </summary>
sealed class DebugQuickInfoSource: ParserServiceDependent, IAsyncQuickInfoSource {

    public DebugQuickInfoSource(ITextBuffer textBuffer): base(textBuffer) {
    }

    /// <summary>
    /// VS-SDK-Vertrag: baut den Debug-Tooltip zum Token unter dem Trigger-Punkt (exakter Lookup via
    /// <c>FindAtPosition</c>). Liefert nur ein Ergebnis, wenn <c>Strg+Umschalt</c> gedrückt ist und an der
    /// Position ein echtes Token liegt; sonst <c>null</c>.
    /// </summary>
    public async Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken) {

        await Task.Yield().ConfigureAwait(false);
        if (cancellationToken.IsCancellationRequested) {
            return null;
        }

        var syntaxTreeAndSnapshot = ParserService.SyntaxTreeAndSnapshot;
        if (syntaxTreeAndSnapshot == null) {
            return null;
        }

        // Map the trigger point down to our buffer.
        SnapshotPoint? triggerPoint = session.GetTriggerPoint(syntaxTreeAndSnapshot.Snapshot);
        if (triggerPoint == null) {
            return null;
        }

        var triggerToken = syntaxTreeAndSnapshot.SyntaxTree.Tokens.FindAtPosition(triggerPoint.Value.Position);

        // Bewusst der EXAKTE Lookup (FindAtPosition), nicht das owning FindToken: das Debug-QuickInfo zeigt das
        // Token genau unter dem Cursor. An einer Trivia-Position liefert FindAtPosition kein Token (Missing) —
        // sobald die Trivia nicht mehr im flachen Token-Strom liegt; dann gibt es hier nichts zu zeigen, der
        // Missing-Zweig fängt das ab.
        if (triggerToken.IsMissing || triggerToken.Parent == null) {
            return null;
        }

        var applicableToSpan = syntaxTreeAndSnapshot.Snapshot.CreateTrackingSpan(
            triggerToken.Start,
            triggerToken.Length,
            SpanTrackingMode.EdgeExclusive);

        var location  = triggerToken.GetLocation();
        var qiContent = $"{triggerToken.Type} ({triggerToken.Classification}) Ln {location?.StartLine + 1} Ch {location?.StartCharacter + 1}\r\n{triggerToken.Parent?.GetType().Name}";

        var qiItemitem = new QuickInfoItem(applicableToSpan: applicableToSpan,
                                           item: qiContent
        );

        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        var modifier = ModifierKeys.Control | ModifierKeys.Shift;
        if ((Keyboard.Modifiers & modifier) != modifier) {
            return null;
        }

        return qiItemitem;
    }

}