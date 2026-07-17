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

/// <summary>
/// Setzt das Ctrl-Klick-GoTo an der Maus um: bei gedrückter Strg-Taste unterstreicht er das navigierbare
/// Symbol unter dem Mauszeiger und zeigt den Hand-Cursor; beim Loslassen der linken Maustaste springt er
/// zum Sprungziel. Die navigierbaren Stellen liest er als <see cref="GoToTag"/>-Tags über einen
/// Tag-Aggregator (befüllt vom <see cref="GoToTagger"/>); den Strg-Zustand bezieht er aus dem geteilten
/// <see cref="ModifierKeyState"/>; die Unterstreichung setzt er über den <see cref="UnderlineTagger"/>;
/// den Sprung selbst führt der <see cref="GoToLocationService"/> aus (in einen Vorschau-Tab). Wird vom
/// <see cref="GoToMouseProcessorProvider"/> je Sicht als Singleton erzeugt.
/// </summary>
sealed class GoToMouseProcessor: MouseProcessorBase {

    readonly IWpfTextView            _textView;
    readonly GoToLocationService     _goToLocationService;
    readonly ITagAggregator<GoToTag> _tagAggregator;
    readonly ModifierKeyState        _keyState;

    Cursor _overriddenCursor;

    [CanBeNull] ITagSpan<GoToTag> _navigateToTagSpan;

    /// <summary>
    /// Verdrahtet den Prozessor: erstellt den <see cref="GoToTag"/>-Tag-Aggregator für die Sicht, bezieht
    /// den geteilten <see cref="ModifierKeyState"/> und abonniert Fokus-Verlust der Sicht sowie
    /// Änderungen des Strg-Zustands.
    /// </summary>
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

    /// <summary>Liefert den (bei Bedarf erzeugten) Singleton-Prozessor für <paramref name="textView"/>.</summary>
    public static GoToMouseProcessor GetMouseProcessorForView(IWpfTextView textView,
                                                              TextViewConnectionListener textViewConnectionListener,
                                                              IViewTagAggregatorFactoryService viewTagAggregatorFactoryService,
                                                              GoToLocationService goToLocationService) {

        return textView.Properties.GetOrCreateSingletonProperty(() => new GoToMouseProcessor(textView, textViewConnectionListener, viewTagAggregatorFactoryService, goToLocationService));
    }

    /// <summary>Entfernt den Prozessor von der Sicht, gibt den Tag-Aggregator frei und meldet die Abonnements ab (beim Trennen der Sicht).</summary>
    void RemoveMouseProcessorForView(IWpfTextView textView) {
        textView.Properties.RemoveProperty(GetType());
        _tagAggregator.Dispose();
        _textView.LostAggregateFocus -= OnTextViewLostAggregateFocus;
        _keyState.KeyStateChanged    -= OnKeyStateChanged;
    }

    /// <summary>Prüft bei jeder Mausbewegung, ob unter dem Zeiger (bei gedrückter Strg-Taste) ein Sprungziel anzubieten ist.</summary>
    public override void PostprocessMouseMove(MouseEventArgs e) {
        ThreadHelper.ThrowIfNotOnUIThread();
        UpdateNavigateToTagSpan();
    }

    #pragma warning disable VSTHRD010
    /// <summary>Löst beim Loslassen der linken Maustaste den Sprung zum aktuell angebotenen Sprungziel aus.</summary>
    public override void PostprocessMouseLeftButtonUp(MouseButtonEventArgs e) {
        NavigateToTagSpan();
    }
    #pragma warning restore VSTHRD010

    /// <summary>Verwirft das angebotene Sprungziel, wenn die Sicht den Fokus verliert.</summary>
    void OnTextViewLostAggregateFocus(object sender, EventArgs e) {
        RemoveNavigateToTagSpan();
    }

    /// <summary>Aktualisiert das Angebot, wenn sich der Strg-Zustand ändert (etwa beim Loslassen von Strg).</summary>
    void OnKeyStateChanged(object sender, EventArgs e) {
        UpdateNavigateToTagSpan();
    }

    /// <summary>
    /// Kernlogik des Angebots: Nur wenn <b>ausschließlich</b> die Strg-Taste gedrückt ist und unter der
    /// Maus ein <see cref="GoToTag"/> liegt, wird dessen Bereich als Sprungziel übernommen; sonst wird ein
    /// laufendes Angebot zurückgenommen.
    /// </summary>
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

    /// <summary>
    /// Übernimmt <paramref name="navigateToTagSpan"/> als aktuelles Sprungziel: unterstreicht den Bereich
    /// (über den <see cref="UnderlineTagger"/>) und setzt den Hand-Cursor. Zeigt der Bereich unverändert
    /// auf dieselbe Stelle, bleibt die Optik bestehen.
    /// </summary>
    void UpdateNavigateToTagSpan(ITagSpan<GoToTag> navigateToTagSpan) {

        if (navigateToTagSpan.Span == _navigateToTagSpan?.Span) {
            // Theoretisch könnten sich die Tags dennoch unterscheiden...
            _navigateToTagSpan = navigateToTagSpan;
            return;
        }

        RemoveNavigateToTagSpan();

        _navigateToTagSpan = navigateToTagSpan;
        UnderlineTagger.GetOrCreateSingelton(_textView.TextBuffer)?.AddUnderlineSpan(navigateToTagSpan.Span);

        _overriddenCursor              = _textView.VisualElement.Cursor;
        _textView.VisualElement.Cursor = Cursors.Hand;
    }

    /// <summary>Nimmt ein laufendes Sprungziel-Angebot zurück: entfernt die Unterstreichung und stellt den ursprünglichen Cursor wieder her.</summary>
    void RemoveNavigateToTagSpan() {

        if (_navigateToTagSpan == null) {
            return;
        }

        UnderlineTagger.GetOrCreateSingelton(_textView.TextBuffer)?.RemoveUnderlineSpan(_navigateToTagSpan.Span);
        _navigateToTagSpan = null;

        _textView.VisualElement.Cursor = _overriddenCursor;
    }

    /// <summary>
    /// Führt den Sprung aus: wechselt auf den UI-Thread, nimmt das Angebot zurück und lässt den
    /// <see cref="GoToLocationService"/> die Sprungziele des <see cref="GoToTag"/> in einem Vorschau-Tab
    /// öffnen — verankert an der Bildschirmgeometrie des angeklickten Bereichs (für ein etwaiges
    /// Auswahl-Popup bei mehreren Zielen).
    /// </summary>
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
