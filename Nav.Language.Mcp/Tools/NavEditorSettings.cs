#region Using Directives

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.Mcp.Tools;

/// <summary>
/// Editor-Einstellungen für die Refactoring-Engine (Rename, Code-Aktionen) und den Formatter — analog zum
/// LSP-Server. TabSize 4 (VS-Default; der Formatter reicht eine abweichende Aufrufer-Angabe durch); der
/// Zeilenumbruch wird aus dem Dokument erkannt (CRLF vs. LF), damit neu komponierte mehrzeilige Texte zum
/// Dokument passen.
/// </summary>
static class NavEditorSettings {

    public static TextEditorSettings For(SourceText sourceText, int tabSize = 4) {
        var newLine = sourceText.Text.Contains("\r\n") ? "\r\n" : "\n";
        return new TextEditorSettings(tabSize: tabSize, newLine: newLine);
    }

}
