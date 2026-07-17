#region Using Directives

using System;
using System.Windows.Input;

using Microsoft.VisualStudio.Text.Editor;
using Pharmatechnik.Nav.Language.Extension.Common;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.GoTo; 

/// <summary>
/// Der je Editor-Sicht geteilte Zustand der Modifikatortasten für das Ctrl-Klick-GoTo. Der
/// <see cref="GoToKeyProcessor"/> aktualisiert ihn bei jedem Tastendruck, der <see cref="GoToMouseProcessor"/>
/// fragt ihn ab (und lauscht auf <see cref="KeyStateChanged"/>), um zu entscheiden, ob bei gedrückter
/// Strg-Taste ein Sprungziel unter der Maus angeboten wird. Als benannte Singleton-Eigenschaft an der
/// Sicht gehalten, damit Key- und Mouse-Processor dieselbe Instanz sehen.
/// </summary>
sealed class ModifierKeyState {

    ModifierKeyState(IWpfTextView textView, TextViewConnectionListener textViewConnectionListener) {
        textViewConnectionListener.AddDisconnectAction(textView, RemoveStateForView);
    }

    /// <summary>Wird ausgelöst, wenn sich der Modifikatortasten-Zustand geändert haben könnte (nach jedem Tastendruck).</summary>
    public event EventHandler<EventArgs> KeyStateChanged;
   
    /// <summary>Die aktuell gedrückten Modifikatortasten (direkt aus <see cref="Keyboard.Modifiers"/>).</summary>
    public ModifierKeys ModifierKeys {
        get { return Keyboard.Modifiers; }
    }

    /// <summary>Ob die Strg-Taste gedrückt ist (unabhängig von weiteren Modifikatoren).</summary>
    public bool IsModifierKeyControlPressed {
        get {
            var modifier = ModifierKeys.Control;
            return (ModifierKeys & modifier) == modifier;
        }
    }

    /// <summary>Ob <b>ausschließlich</b> die Strg-Taste gedrückt ist — die Bedingung für das GoTo-Angebot.</summary>
    public bool IsOnlyModifierKeyControlPressed {
        get { return ModifierKeys == ModifierKeys.Control; }
    }

    /// <summary>Meldet eine mögliche Zustandsänderung; löst <see cref="KeyStateChanged"/> aus.</summary>
    public void UpdateState() {
        KeyStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Liefert die (bei Bedarf erzeugte) Singleton-Instanz für <paramref name="textView"/>.</summary>
    public static ModifierKeyState GetStateForView(IWpfTextView textView, TextViewConnectionListener textViewConnectionListener) {
        return textView.Properties.GetOrCreateSingletonProperty(() => new ModifierKeyState(textView, textViewConnectionListener));
    }

    /// <summary>Entfernt den Zustand von der Sicht (beim Trennen der Sicht aufgerufen).</summary>
    public void RemoveStateForView(IWpfTextView textView) {
        textView.Properties.RemoveProperty(GetType());
    }
}
