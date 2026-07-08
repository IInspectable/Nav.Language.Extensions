#region Using Directives

using System;
using System.Windows.Input;

using JetBrains.Annotations;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;

using Pharmatechnik.Nav.Language.Extension.Common;
using Pharmatechnik.Nav.Language.Extension.GoToLocation;
using Pharmatechnik.Nav.Language.Extension.Underlining;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.GoTo; 

sealed class GoToMouseProcessor: MouseProcessorBase {

    readonly IWpfTextView            _textView;
    readonly GoToLocationService     _goToLocationService;
    readonly ITagAggregator<GoToTag> _tagAggregator;
    readonly ModifierKeyState        _keyState;

    Cursor _overriddenCursor;

    [CanBeNull] ITagSpan<GoToTag> _navigateToTagSpan;

    GoToMouseProcessor(IWpfTextView textView,
                       TextViewConnectionListener textViewConnectionListener,
                       IViewTagAggregatorFactoryService viewTagAggregatorFactoryService,
                       GoToLocationService goToLocationService) {
        _textView            = textView;
        _goToLocationService = goToLocationService;
        _tagAggregator       = viewTagAggregatorFactoryService.CreateTagAggregator<GoToTag>(textView);
        _keyState            = ModifierKeyState.GetStateForView(textView, textViewConnectionListener);

        _textView.LostAggregateFocus += OnTextViewLostAggregateFocus;
        _keyState.KeyStateChanged    += OnKeyStateChanged;

        textViewConnectionListener.AddDisconnectAction(textView, RemoveMouseProcessorForView);
    }

    public static GoToMouseProcessor GetMouseProcessorForView(IWpfTextView textView,
                                                              TextViewConnectionListener textViewConnectionListener,
                                                              IViewTagAggregatorFactoryService viewTagAggregatorFactoryService,
                                                              GoToLocationService goToLocationService) {

        return textView.Properties.GetOrCreateSingletonProperty(() => new GoToMouseProcessor(textView, textViewConnectionListener, viewTagAggregatorFactoryService, goToLocationService));
    }

    void RemoveMouseProcessorForView(IWpfTextView textView) {
        textView.Properties.RemoveProperty(GetType());
        _tagAggregator.Dispose();
        _textView.LostAggregateFocus -= OnTextViewLostAggregateFocus;
        _keyState.KeyStateChanged    -= OnKeyStateChanged;
    }

    public override void PostprocessMouseMove(MouseEventArgs e) {
        ThreadHelper.ThrowIfNotOnUIThread();
        UpdateNavigateToTagSpan();
    }

    #pragma warning disable VSTHRD010
    public override void PostprocessMouseLeftButtonUp(MouseButtonEventArgs e) {
        NavigateToTagSpan();
    }
    #pragma warning restore VSTHRD010

    void OnTextViewLostAggregateFocus(object sender, EventArgs e) {
        RemoveNavigateToTagSpan();
    }

    void OnKeyStateChanged(object sender, EventArgs e) {
        UpdateNavigateToTagSpan();
    }

    void UpdateNavigateToTagSpan() {

        if (!_keyState.IsOnlyModifierKeyControlPressed) {
            RemoveNavigateToTagSpan();
            return;
        }

        var navigateToTagSpan = _textView.GetGoToDefinitionTagSpanAtMousePosition(_tagAggregator);

        if (navigateToTagSpan != null) {
            UpdateNavigateToTagSpan(navigateToTagSpan);
        } else {
            RemoveNavigateToTagSpan();
        }
    }

    void UpdateNavigateToTagSpan(ITagSpan<GoToTag> navigateToTagSpan) {

        if (navigateToTagSpan.Span == _navigateToTagSpan?.Span) {
            // Theoretisch k�nnten sich die Tags dennoch unterscheiden...
            _navigateToTagSpan = navigateToTagSpan;
            return;
        }

        RemoveNavigateToTagSpan();

        _navigateToTagSpan = navigateToTagSpan;
        UnderlineTagger.GetOrCreateSingelton(_textView.TextBuffer)?.AddUnderlineSpan(navigateToTagSpan.Span);

        _overriddenCursor              = _textView.VisualElement.Cursor;
        _textView.VisualElement.Cursor = Cursors.Hand;
    }

    void RemoveNavigateToTagSpan() {

        if (_navigateToTagSpan == null) {
            return;
        }

        UnderlineTagger.GetOrCreateSingelton(_textView.TextBuffer)?.RemoveUnderlineSpan(_navigateToTagSpan.Span);
        _navigateToTagSpan = null;

        _textView.VisualElement.Cursor = _overriddenCursor;
    }

    void NavigateToTagSpan() {

        NavLanguagePackage.Jtf.RunAsync(async () => {

            if (_navigateToTagSpan == null) {
                return;
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            _textView.Selection.Clear();

            var tagSpan = _navigateToTagSpan;
            RemoveNavigateToTagSpan();

            var placementRectangle = _textView.TextViewLines.GetTextMarkerGeometry(tagSpan.Span).Bounds;

            placementRectangle.Offset(-_textView.ViewportLeft, -_textView.ViewportTop);

            await _goToLocationService.GoToLocationInPreviewTabAsync(
                _textView,
                placementRectangle,
                tagSpan.Tag.Provider);
        }).FileAndForget("nav/gotomouseprocessor/navigate");
    }

}