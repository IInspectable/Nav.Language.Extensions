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

static class WpfTextViewExtensions {

    public static SnapshotPoint? GetBufferPositionAtMousePosition(this IWpfTextView textView) {

        Point position = Mouse.GetPosition(textView.VisualElement);

        var viewportPoint = textView.ToViewportPoint(position);
        var line          = textView.TextViewLines.GetTextViewLineContainingYCoordinate(viewportPoint.Y);
        var bufferPos     = line?.GetBufferPositionFromXCoordinate(viewportPoint.X, true);

        return bufferPos;
    }

    public static Point ToViewportPoint(this IWpfTextView textView, Point position) {
        return new Point(position.X + textView.ViewportLeft, position.Y + textView.ViewportTop);
    }

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

    [CanBeNull]
    public static ITagSpan<GoToTag> GetGoToDefinitionTagSpanAtMousePosition(this IWpfTextView textView, ITagAggregator<GoToTag> tagAggregator) {

        var spanAtMousePos = textView.GetBufferPositionAtMousePosition().ToSnapshotSpan();
        if (spanAtMousePos == null) {
            return null;
        }

        var mappingTagSpan = tagAggregator.GetTags(spanAtMousePos.Value).FirstOrDefault();

        return textView.MapToSingleSnapshotSpan(mappingTagSpan);
    }

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
            });
        };
    }

    static bool IsNormal(double value) {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}