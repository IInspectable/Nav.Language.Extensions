#region Using Directives

using System.Windows.Input;
using Microsoft.VisualStudio.Text.Editor;
using Pharmatechnik.Nav.Language.Extension.Common;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.GoTo; 

/// <summary>
/// Der <see cref="KeyProcessor"/> des Ctrl-Klick-GoTo: aktualisiert bei jedem Tastendruck den
/// <see cref="ModifierKeyState"/> der Sicht, damit der <see cref="GoToMouseProcessor"/> das Drücken und
/// Loslassen der Strg-Taste mitbekommt (Ein-/Ausblenden der Unterstreichung und des Hand-Cursors). Wird
/// vom <see cref="GoToKeyProcessorProvider"/> je Sicht als Singleton erzeugt.
/// </summary>
sealed class GoToKeyProcessor: KeyProcessor {

    readonly ModifierKeyState _keyState;

    GoToKeyProcessor(IWpfTextView textView, TextViewConnectionListener textViewConnectionListener) {
        _keyState = ModifierKeyState.GetStateForView(textView, textViewConnectionListener);
        textViewConnectionListener.AddDisconnectAction(textView, RemoveKeyProcessorForView);
    }

    /// <summary>Liefert den (bei Bedarf erzeugten) Singleton-Prozessor für <paramref name="textView"/>.</summary>
    public static GoToKeyProcessor GetKeyProcessorForView(IWpfTextView textView, TextViewConnectionListener textViewConnectionListener) {
        return textView.Properties.GetOrCreateSingletonProperty(() => new GoToKeyProcessor(textView, textViewConnectionListener));
    }

    /// <summary>Entfernt den Prozessor von der Sicht (beim Trennen der Sicht aufgerufen).</summary>
    void RemoveKeyProcessorForView(IWpfTextView textView) {
        textView.Properties.RemoveProperty(GetType());
    }

    /// <summary>Aktualisiert bei jedem Tastendruck den Modifikatortasten-Zustand.</summary>
    public override void PreviewKeyDown(KeyEventArgs args) {
        _keyState.UpdateState();
    }

    /// <summary>Aktualisiert beim Loslassen jeder Taste den Modifikatortasten-Zustand.</summary>
    public override void PreviewKeyUp(KeyEventArgs args) {
        _keyState.UpdateState();
    }        
}
