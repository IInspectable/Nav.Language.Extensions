#region Using Directives

using System.Linq;
using System.Windows;
using System.Windows.Input;
using JetBrains.Annotations;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;

using Pharmatechnik.Nav.Language.Extension.GoToLocation;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Common; 

/// <summary>
/// Erweiterungsmethoden über <see cref="IWpfTextView"/> mit WPF-Bezug: die Puffer-Position unter dem
/// Mauszeiger ermitteln, Tag-Spans (insbesondere <see cref="GoToTag"/>) an Maus- oder Caret-Position
/// auflösen und eine eingebettete Ansicht auf ihren Inhalt einpassen.
/// </summary>
static class WpfTextViewExtensions {

    /// <summary>
    /// Ermittelt die <see cref="SnapshotPoint"/> im Puffer, die unter der aktuellen Mausposition
    /// liegt, oder <see langword="null"/>.
    /// </summary>
    /// <param name="textView">Die Ansicht.</param>
    /// <returns>Die Puffer-Position unter der Maus, oder <see langword="null"/>.</returns>
    public static SnapshotPoint? GetBufferPositionAtMousePosition(this IWpfTextView textView) {

        Point position = Mouse.GetPosition(textView.VisualElement);

        var viewportPoint = textView.ToViewportPoint(position);
        var line          = textView.TextViewLines.GetTextViewLineContainingYCoordinate(viewportPoint.Y);
        var bufferPos     = line?.GetBufferPositionFromXCoordinate(viewportPoint.X, true);

        return bufferPos;
    }

    /// <summary>
    /// Rechnet einen relativen <paramref name="position"/>-Punkt in Viewport-Koordinaten um (unter
    /// Berücksichtigung von <see cref="ITextView.ViewportLeft"/>/<c>ViewportTop</c>).
    /// </summary>
    /// <param name="textView">Die Ansicht.</param>
    /// <param name="position">Der umzurechnende Punkt relativ zum sichtbaren Bereich.</param>
    /// <returns>Der Punkt in Viewport-Koordinaten.</returns>
    public static Point ToViewportPoint(this IWpfTextView textView, Point position) {
        return new Point(position.X + textView.ViewportLeft, position.Y + textView.ViewportTop);
    }

    /// <summary>
    /// Bildet <paramref name="mappingTagSpan"/> auf den aktuellen Snapshot der Ansicht ab und liefert
    /// den ersten resultierenden <see cref="ITagSpan{T}"/>, oder <see langword="null"/>.
    /// </summary>
    /// <typeparam name="T">Der Tag-Typ.</typeparam>
    /// <param name="textView">Die Ansicht.</param>
    /// <param name="mappingTagSpan">Der abzubildende Mapping-Tag-Span.</param>
    /// <returns>Der abgebildete Tag-Span, oder <see langword="null"/>.</returns>
    [CanBeNull]
    public static ITagSpan<T> MapToSingleSnapshotSpan<T>(this IWpfTextView textView, IMappingTagSpan<T> mappingTagSpan) where T: ITag {

        if(mappingTagSpan == null || textView.TextSnapshot ==null) {
            return null;
        }

        var tagSpans = mappingTagSpan.Span.GetSpans(textView.TextSnapshot);
        if (!tagSpans.Any()) {
            return null;
        }

        return new TagSpan<T>(tagSpans[0], mappingTagSpan.Tag);
    }

    /// <summary>
    /// Liefert den <see cref="GoToTag"/>-Tag-Span unter der Mausposition (über
    /// <paramref name="tagAggregator"/>), oder <see langword="null"/> — die Grundlage für die
    /// Ctrl-Klick-Navigation an der Maus.
    /// </summary>
    /// <param name="textView">Die Ansicht.</param>
    /// <param name="tagAggregator">Der Aggregator der <see cref="GoToTag"/>.</param>
    /// <returns>Der Go-To-Tag-Span unter der Maus, oder <see langword="null"/>.</returns>
    [CanBeNull]
    public static ITagSpan<GoToTag> GetGoToDefinitionTagSpanAtMousePosition(this IWpfTextView textView, ITagAggregator<GoToTag> tagAggregator) {

        var spanAtMousePos = textView.GetBufferPositionAtMousePosition().ToSnapshotSpan();
        if (spanAtMousePos == null) {
            return null;
        }

        var mappingTagSpan = tagAggregator.GetTags(spanAtMousePos.Value).FirstOrDefault();

        return textView.MapToSingleSnapshotSpan(mappingTagSpan);
    }

    /// <summary>
    /// Liefert den <see cref="GoToTag"/>-Tag-Span an der Caret-Position (über
    /// <paramref name="tagAggregator"/>), oder <see langword="null"/>. Steht das Caret unmittelbar
    /// hinter einem Bezeichner, wird ein Zeichen zurückgegangen, damit das Definition-Tag noch
    /// getroffen wird.
    /// </summary>
    /// <param name="textView">Die Ansicht.</param>
    /// <param name="tagAggregator">Der Aggregator der <see cref="GoToTag"/>.</param>
    /// <returns>Der Go-To-Tag-Span am Caret, oder <see langword="null"/>.</returns>
    [CanBeNull]
    public static ITagSpan<GoToTag> GetGoToDefinitionTagSpanAtCaretPosition(this IWpfTextView textView, ITagAggregator<GoToTag> tagAggregator) {

        var caretPosition = textView.Caret.Position.BufferPosition;
            
        // Wenn das Caret am Ende einer "Definition" steht, gehen wir ein Zeichen zurück, damit das "Definition-Tag" auch gefunden wird
        // Bsp.: Foo|; <= Das Caret steht hinter Foo. Nach der Adaption: Fo|o; <= jetzt wird Foo potentiell gefunden
        if (!SyntaxFacts.IsIdentifierCharacter(caretPosition.GetChar()) 
         && caretPosition > caretPosition.GetContainingLine().Start &&
            SyntaxFacts.IsIdentifierCharacter((caretPosition -1).GetChar())) {
            caretPosition = caretPosition -1;
        }
            
        var spanAtCaretPos = caretPosition.ToSnapshotSpan();
        var mappingTagSpan = tagAggregator.GetTags(spanAtCaretPos).FirstOrDefault();

        return textView.MapToSingleSnapshotSpan(mappingTagSpan);
    }

    /// <summary>
    /// Richtet die eingebettete Ansicht so ein, dass sie sich bei jedem Layout-Wechsel auf Höhe und
    /// Breite ihres Inhalts einpasst (für in QuickInfo/Presenter eingebettete Code-Ansichten).
    /// </summary>
    /// <param name="view">Die einzupassende Ansicht.</param>
    public static void PrepareSizeToFit(this IWpfTextView view) {
        view.LayoutChanged += (_, _) => {
            NavLanguagePackage.Jtf.RunAsync(async () => {

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                view.VisualElement.Height = view.LineHeight * view.TextBuffer.CurrentSnapshot.LineCount;
                double width = view.VisualElement.Width;
                if (!IsNormal(view.MaxTextRightCoordinate))
                    return;
                if (IsNormal(width) && view.MaxTextRightCoordinate <= width)
                    return;

                view.VisualElement.Width = view.MaxTextRightCoordinate;
            }).FileAndForget("nav/wpftextview/preparesizetofit");
        };
    }

    /// <summary>Prüft, ob <paramref name="value"/> eine reguläre Zahl ist (weder <c>NaN</c> noch unendlich).</summary>
    static bool IsNormal(double value) {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}