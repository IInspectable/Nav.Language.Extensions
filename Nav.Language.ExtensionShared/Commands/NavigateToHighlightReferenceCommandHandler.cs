#region Using Directives

using System.Linq;
using System.Collections.Generic;
using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Outlining;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Utilities;

using Pharmatechnik.Nav.Language.Extension.Common;
using Pharmatechnik.Nav.Language.Extension.HighlightReferences;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Commands; 

/// <summary>
/// Command-Handler für „Go To Next/Previous Highlighted Reference" (Ctrl+Shift+Pfeil auf/ab) in Nav-Dateien.
/// Springt zwischen den vom Referenz-Highlighting markierten Stellen (<see cref="ReferenceHighlightTag"/>) und
/// bewegt den Cursor zyklisch zur nächsten bzw. vorherigen Fundstelle, wobei etwaige Outlining-Regionen
/// aufgeklappt werden.
/// </summary>
[Export(typeof(ICommandHandler))]
[ContentType(NavLanguageContentDefinitions.ContentType)]
[Name(CommandHandlerNames.NavigateToHighlightReferenceCommandHandler)]
class NavigateToHighlightReferenceCommandHandler:
    ICommandHandler<NavigateToNextHighlightedReferenceCommandArgs>,
    ICommandHandler<NavigateToPreviousHighlightedReferenceCommandArgs> {

    readonly IViewTagAggregatorFactoryService _tagAggregatorFactory;
    readonly IOutliningManagerService         _outliningManagerService;

    [ImportingConstructor]
    public NavigateToHighlightReferenceCommandHandler(IViewTagAggregatorFactoryService tagAggregatorFactory, IOutliningManagerService outliningManagerService) {
        _tagAggregatorFactory    = tagAggregatorFactory;
        _outliningManagerService = outliningManagerService;
    }

    /// <summary>Im Command-System angezeigter Name des Befehls.</summary>
    public string DisplayName => "Go To Next/Previous Member";

    /// <summary>„Go To Previous" ist stets verfügbar.</summary>
    public CommandState GetCommandState(NavigateToPreviousHighlightedReferenceCommandArgs args) {
        return CommandState.Available;
    }

    /// <summary>Springt zur vorherigen markierten Referenz (<see cref="NavigateDirection.Up"/>).</summary>
    public bool ExecuteCommand(NavigateToPreviousHighlightedReferenceCommandArgs args, CommandExecutionContext executionContext) {
        return ExecuteCommand(args.TextView, NavigateDirection.Up);
    }

    /// <summary>„Go To Next" ist stets verfügbar.</summary>
    public CommandState GetCommandState(NavigateToNextHighlightedReferenceCommandArgs args) {
        return CommandState.Available;
    }

    /// <summary>Springt zur nächsten markierten Referenz (<see cref="NavigateDirection.Down"/>).</summary>
    public bool ExecuteCommand(NavigateToNextHighlightedReferenceCommandArgs args, CommandExecutionContext executionContext) {
        return ExecuteCommand(args.TextView, NavigateDirection.Down);
    }

    /// <summary>Sprungrichtung zwischen den markierten Referenzen; der Wert dient zugleich als Index-Offset.</summary>
    enum NavigateDirection {

        /// <summary>Zur vorherigen Fundstelle (Index −1).</summary>
        Up   = -1,
        /// <summary>Zur nächsten Fundstelle (Index +1).</summary>
        Down = 1,

    }

    /// <summary>
    /// Kern der Navigation: bestimmt die Fundstelle unter dem Cursor, berechnet die Zielspanne in
    /// <paramref name="direction"/> (zyklisch am Rand umlaufend) und bewegt den Cursor dorthin.
    /// </summary>
    bool ExecuteCommand(ITextView textView, NavigateDirection direction) {
        var wpfTextView = textView as IWpfTextView;
        if (wpfTextView == null) {
            return false;
        }

        using var tagger         = _tagAggregatorFactory.CreateTagAggregator<ReferenceHighlightTag>(wpfTextView);
        var       tagUnderCursor = FindTagUnderCaret(tagger, wpfTextView);

        if (tagUnderCursor == null) {
            return false;
        }

        var spans = GetReferenceSpans(tagger, wpfTextView.TextSnapshot.GetFullSpan()).ToList();

        var destinationSpan = GetDestinationSpan(tagUnderCursor.Value, spans, direction);
        if (wpfTextView.TryMoveCaretToAndEnsureVisible(destinationSpan.Start, _outliningManagerService)) {
            wpfTextView.SetSelection(destinationSpan);
        }

        return true;

    }

    /// <summary>Liefert die von <see cref="ReferenceHighlightTag"/>-Tags markierten Spannen im Bereich <paramref name="span"/>, nach Startposition sortiert.</summary>
    static IList<SnapshotSpan> GetReferenceSpans(ITagAggregator<ReferenceHighlightTag> tagAggregator, SnapshotSpan span) {
        return tagAggregator.GetTags(span)
                            .SelectMany(tag => tag.Span.GetSpans(span.Snapshot.TextBuffer))
                            .OrderBy(tag => tag.Start)
                            .ToList();
    }

    /// <summary>
    /// Ermittelt aus der sortierten Trefferliste die Zielspanne relativ zur Fundstelle unter dem Cursor in der
    /// gewünschten <paramref name="direction"/>; über den Listenrand hinaus wird zyklisch umgebrochen.
    /// </summary>
    static SnapshotSpan GetDestinationSpan(SnapshotSpan tagUnderCursor, List<SnapshotSpan> orderedTagSpans, NavigateDirection direction) {

        var destIndex = orderedTagSpans.BinarySearch(tagUnderCursor, new StartComparer());

        destIndex += direction == NavigateDirection.Down ? 1 : -1;
        if (destIndex < 0) {
            destIndex = orderedTagSpans.Count - 1;
        } else if (destIndex == orderedTagSpans.Count) {
            destIndex = 0;
        }

        return orderedTagSpans[destIndex];
    }

    /// <summary>Liefert die markierte Referenz an der aktuellen Cursor-Position, oder <see langword="null"/>, wenn dort keine liegt.</summary>
    SnapshotSpan? FindTagUnderCaret(ITagAggregator<ReferenceHighlightTag> tagAggregator, ITextView textView) {
        // We always want to be working with the surface buffer here, so this line is correct
        var caretPosition = textView.Caret.Position.BufferPosition.Position;

        var tags = GetReferenceSpans(tagAggregator, new SnapshotSpan(textView.TextSnapshot, new Span(caretPosition, 0)));
        return tags.Any()
            ? tags.First()
            : null;
    }

    /// <summary>Vergleicht zwei <see cref="SnapshotSpan"/>s anhand ihrer Startposition (für die Binärsuche der Fundstellen).</summary>
    sealed class StartComparer: IComparer<SnapshotSpan> {

        /// <summary>Vergleicht die Startpositionen von <paramref name="x"/> und <paramref name="y"/>.</summary>
        public int Compare(SnapshotSpan x, SnapshotSpan y) {
            return x.Start.CompareTo(y.Start);
        }

    }

}