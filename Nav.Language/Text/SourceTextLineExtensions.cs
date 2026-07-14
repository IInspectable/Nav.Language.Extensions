using System;

namespace Pharmatechnik.Nav.Language.Text;

/// <summary>
/// Zeilenbezogene, tabulator-bewusste Spalten-Helfer für <see cref="SourceTextLine"/>. Sie setzen auf den
/// gleichnamigen Span-Operationen aus <see cref="StringExtensions"/> auf und rechnen dabei den
/// Zeilen-<see cref="SourceTextLine.Span"/> aus.
/// </summary>
public static class SourceTextLineExtensions {

    /// <summary>
    /// Liefert den Einzug der Zeile als reine Leerzeichen-Kette — die Tabulatoren des tatsächlichen Einzugs
    /// werden gemäß <paramref name="tabSize"/> in Spalten umgerechnet
    /// (<see cref="GetSignificantColumn"/>) und durch ebenso viele Leerzeichen ersetzt.
    /// </summary>
    /// <param name="sourceText">Die Zeile, deren Einzug ermittelt wird.</param>
    /// <param name="tabSize">Die Tabulatorweite in Spalten.</param>
    public static string GetIndentAsSpaces(this SourceTextLine sourceText, int tabSize) {

        var startColumn = sourceText.GetSignificantColumn(tabSize);

        return new String(' ', startColumn);
    }

    /// <summary>
    /// Liefert den Spaltenindex (beginnend bei 0) für den angegebenen Offset vom Start der Zeile.
    /// Es werden Tabulatoren entsprechend eingerechnet.
    /// </summary>
    /// <example>
    /// Gegeben sei folgende Zeile mit gemischten Leerzeichen (o) und Tabulatoren (->) mit einer Tabulatorweite
    /// von 4 und anschließendem Text (T). Der angeforderte Offset ist 4:
    /// TT->--->TTTTTT
    /// ^^-^---^
    /// Der Spaltenindex für den Zeichenindex 4 ist 8 (man beachte die 2 Tabulatoren!).
    /// </example>
    public static int GetColumnForOffset(this SourceTextLine sourceText, int tabSize, int charPositionInLine) {
        return sourceText.Span.GetColumnForOffset(tabSize, charPositionInLine);
    }

    /// <summary>
    /// Liefert den Spaltenindex (beginnend bei 0) für das erste Signifikante Zeichen in der angegebenen Zeile.
    /// Als nicht signifikant gelten alle Arten von Leerzeichen. Dabei werden Tabulatoren entsprechend umgerechnet.
    /// </summary>
    /// <example>
    /// Gegeben sei folgende Zeile mit gemischten Leerzeichen (o) und Tabulatoren (->) mit einer Tabulatorweite
    /// von 4 und anschließendem Text (T):
    /// --->oo->TTTTTT
    /// --------^
    /// Der Signifikante Spaltenindex für diese Zeile ist 8.
    /// </example>
    public static int GetSignificantColumn(this SourceTextLine sourceText, int tabSize) {
        return sourceText.Span.GetSignificantColumn(tabSize);
    }

}
