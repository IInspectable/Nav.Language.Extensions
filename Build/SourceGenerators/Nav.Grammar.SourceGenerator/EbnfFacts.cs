using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Pharmatechnik.Nav.Language.Grammar.SourceGenerator;

/// <summary>
/// Leichtgewichtige, rein textuelle Analyse der EBNF-Fragmente — genug, um Vollständigkeit (NAV001)
/// und Geschlossenheit (NAV002) der Grammatik zu prüfen. Notation (siehe NavParser-Klassendoku):
/// <list type="bullet">
///   <item><description><c>"…"</c> — literales Terminal,</description></item>
///   <item><description><c>(* … *)</c> — erläuternder Kommentar,</description></item>
///   <item><description>groß beginnende Bezeichner (<c>Identifier</c>, <c>StringLiteral</c>, <c>EOF</c>)
///   — kategorische Terminale,</description></item>
///   <item><description>klein beginnende Bezeichner (<c>camelCase</c>) — Nichtterminale.</description></item>
/// </list>
/// </summary>
static class EbnfFacts {

    static readonly Regex CommentRegex    = new(@"\(\*.*?\*\)", RegexOptions.Singleline);
    static readonly Regex StringRegex     = new("\"[^\"]*\"");
    static readonly Regex LeftHandSide    = new(@"(?m)^[ \t]*([A-Za-z][A-Za-z0-9]*)[ \t]*::=");
    static readonly Regex IdentifierRegex = new(@"[A-Za-z][A-Za-z0-9]*");

    /// <summary>Alle Nichtterminale, die das Fragment <i>definiert</i> (jede linke Seite vor <c>::=</c>).
    /// Ein Fragment kann mehrere Produktionen enthalten (z.B. <c>codeType</c> + <c>arrayType</c>).</summary>
    public static IEnumerable<string> DefinedNames(string ebnf) {
        return LeftHandSide.Matches(ebnf).Cast<Match>().Select(m => m.Groups[1].Value);
    }

    /// <summary>Alle Nichtterminale, die das Fragment auf einer rechten Seite <i>referenziert</i> —
    /// nach Entfernen von Kommentaren, literalen Terminalen und den linken Seiten.</summary>
    public static IEnumerable<string> ReferencedNonterminals(string ebnf) {

        var withoutComments = CommentRegex.Replace(ebnf, " ");
        var withoutStrings  = StringRegex.Replace(withoutComments, " ");
        var rightHandSides  = LeftHandSide.Replace(withoutStrings, " ");

        return IdentifierRegex.Matches(rightHandSides)
                              .Cast<Match>()
                              .Select(m => m.Value)
                              .Where(token => char.IsLower(token[0]));
    }

}
