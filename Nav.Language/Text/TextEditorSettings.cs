#region Using Directives

using System;

#endregion

namespace Pharmatechnik.Nav.Language.Text; 

public sealed class TextEditorSettings {

    public TextEditorSettings(int tabSize, string newLine) {
        if (tabSize < 0) {
            throw new ArgumentOutOfRangeException();
        }

        TabSize = tabSize;
        NewLine = newLine ?? throw new ArgumentNullException(nameof(newLine));
    }

    public int    TabSize { get; }
    public string NewLine { get; }

}