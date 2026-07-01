using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Pharmatechnik.Nav.Language.CodeAnalysis.Shared;

/// <summary>
/// Hilfsfunktionen, um strukturierte XML-Dokumentationskommentare zur Compile-Zeit auszuwerten —
/// genutzt von den Quellgeneratoren, um z.B. EBNF-Fragmente aus den <c><![CDATA[...]]></c>-Blöcken
/// der Parser-Methoden zu ziehen.
/// </summary>
/// <remarks>
/// Die Extraktion arbeitet bewusst auf dem <i>rohen</i> Trivia-Text (<see cref="Microsoft.CodeAnalysis.SyntaxNode.GetLeadingTrivia"/>
/// → <c>ToFullString</c>), nicht auf dem strukturierten Doku-Kommentar-Baum: bei einem normalen Build
/// ist <c>DocumentationMode</c> häufig <c>None</c>, dann existieren keine
/// <c>XmlCDataSectionSyntax</c>-Knoten — der rohe Trivia-Text dagegen ist immer vollständig vorhanden.
/// </remarks>
public static class XmlDocExtensions {

    const string CDataOpen  = "<![CDATA[";
    const string CDataClose = "]]>";

    /// <summary>
    /// Liefert den Inhalt des ersten <c><![CDATA[...]]></c>-Blocks im Dokumentationskommentar des
    /// Members, der <c>::=</c> enthält (das EBNF-Fragment einer Grammatikregel), normalisiert auf
    /// LF-Zeilenenden und ohne führende/abschließende Leerzeilen. <c>null</c>, wenn kein solcher Block
    /// existiert.
    /// </summary>
    public static string? GetEbnfFragment(this MemberDeclarationSyntax member) {
        return ExtractEbnfFromDocComment(member.GetLeadingTrivia().ToFullString());
    }

    /// <summary>
    /// Extrahiert das erste <c>::=</c>-haltige CDATA-Fragment aus rohem Doku-Kommentar-Text. Separat
    /// testbar (parse-modus-unabhängig).
    /// </summary>
    public static string? ExtractEbnfFromDocComment(string rawLeadingTrivia) {

        var searchFrom = 0;

        while (true) {

            var open = rawLeadingTrivia.IndexOf(CDataOpen, searchFrom, StringComparison.Ordinal);
            if (open < 0) {
                return null;
            }

            var contentStart = open + CDataOpen.Length;
            var close        = rawLeadingTrivia.IndexOf(CDataClose, contentStart, StringComparison.Ordinal);
            if (close < 0) {
                return null;
            }

            var content  = rawLeadingTrivia.Substring(contentStart, close - contentStart);
            var stripped = StripDocExterior(content);

            if (stripped.Contains("::=")) {
                return Normalize(stripped);
            }

            searchFrom = close + CDataClose.Length;
        }
    }

    // Entfernt je Zeile die führende `///`-Doku-Markierung (plus ein optionales Leerzeichen). Im rohen
    // Trivia-Text trägt jede Zeile innerhalb des CDATA-Blocks noch ihr `///`.
    static string StripDocExterior(string content) {

        var lines = SplitLines(content);

        for (var i = 0; i < lines.Count; i++) {
            var trimmed = lines[i].TrimStart();
            if (trimmed.StartsWith("///", StringComparison.Ordinal)) {
                trimmed = trimmed.Substring(3);
                if (trimmed.StartsWith(" ", StringComparison.Ordinal)) {
                    trimmed = trimmed.Substring(1);
                }

                lines[i] = trimmed;
            }
        }

        return string.Join("\n", lines);
    }

    static string Normalize(string text) {

        var lines = SplitLines(text).Select(l => l.TrimEnd()).ToList();

        while (lines.Count > 0 && lines[0].Length == 0) {
            lines.RemoveAt(0);
        }

        while (lines.Count > 0 && lines[lines.Count - 1].Length == 0) {
            lines.RemoveAt(lines.Count - 1);
        }

        var indent = CommonLeadingWhitespace(lines);
        if (indent > 0) {
            for (var i = 0; i < lines.Count; i++) {
                lines[i] = lines[i].Length >= indent ? lines[i].Substring(indent) : lines[i];
            }
        }

        return string.Join("\n", lines);
    }

    static List<string> SplitLines(string text) {
        return text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n').ToList();
    }

    static int CommonLeadingWhitespace(IReadOnlyList<string> lines) {

        var min = int.MaxValue;

        foreach (var line in lines) {
            if (line.Length == 0) {
                continue;
            }

            var ws = 0;
            while (ws < line.Length && (line[ws] == ' ' || line[ws] == '\t')) {
                ws++;
            }

            if (ws < min) {
                min = ws;
            }
        }

        return min == int.MaxValue ? 0 : min;
    }

}
