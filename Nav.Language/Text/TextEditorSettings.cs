#region Using Directives

using System;

#endregion

namespace Pharmatechnik.Nav.Language.Text;

/// <summary>
/// Die editor-abhängigen Textparameter, die spalten- bzw. zeilenweise Berechnungen brauchen: die
/// Tabulatorweite und die zu verwendende Zeilenumbruch-Sequenz. Wird u.a. vom Formatter
/// (<see cref="Formatting.NavFormattingService.FormatDocument"/>) übergeben und speist die Spaltenrechnung in
/// <see cref="StringExtensions.GetColumnForOffset(ReadOnlySpan{char}, int, int)"/>.
/// </summary>
public sealed class TextEditorSettings {

    /// <summary>
    /// Erzeugt die Einstellungen mit der angegebenen Tabulatorweite und Zeilenumbruch-Sequenz.
    /// </summary>
    /// <param name="tabSize">Die Tabulatorweite in Spalten (nicht negativ).</param>
    /// <param name="newLine">Die zu verwendende Zeilenumbruch-Sequenz (z.B. <c>"\r\n"</c> oder <c>"\n"</c>).</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="tabSize"/> ist negativ.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="newLine"/> ist <c>null</c>.</exception>
    public TextEditorSettings(int tabSize, string newLine) {
        if (tabSize < 0) {
            throw new ArgumentOutOfRangeException();
        }

        TabSize = tabSize;
        NewLine = newLine ?? throw new ArgumentNullException(nameof(newLine));
    }

    /// <summary>Die Tabulatorweite in Spalten.</summary>
    public int    TabSize { get; }
    /// <summary>Die zu verwendende Zeilenumbruch-Sequenz (z.B. <c>"\r\n"</c> oder <c>"\n"</c>).</summary>
    public string NewLine { get; }

}