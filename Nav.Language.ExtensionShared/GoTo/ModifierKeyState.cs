#region Using Directives

using System;
using System.Windows.Input;

using Microsoft.VisualStudio.Text.Editor;
using Pharmatechnik.Nav.Language.Extension.Common;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.GoTo; 

sealed class ModifierKeyState {

    ModifierKeyState(IWpfTextView textView, TextViewConnectionListener textViewConnectionListener) {
        textViewConnectionListener.AddDisconnectAction(textView, RemoveStateForView);
    }

    public event EventHandler<EventArgs> KeyStateChanged;
   
    public ModifierKeys ModifierKeys {
        get { return Keyboard.Modifiers; }
    }

    public bool IsModifierKeyControlPressed {
        get {
            var modifier = ModifierKeys.Control;
            return (ModifierKeys & modifier) == modifier;
        }
    }

    public bool IsOnlyModifierKeyControlPressed {
        get { return ModifierKeys == ModifierKeys.Control; }
    }

    public void UpdateState() {
        KeyStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public static ModifierKeyState GetStateForView(IWpfTextView textView, TextViewConnectionListener textViewConnectionListener) {
        return textView.Properties.GetOrCreateSingletonProperty(() => new ModifierKeyState(textView, textViewConnectionListener));
    }

    public void RemoveStateForView(IWpfTextView textView) {
        textView.Properties.RemoveProperty(GetType());
    }
}