#region Using Directives

using System.Collections.Generic;
using System.Linq;

using Pharmatechnik.Nav.Language;

#endregion

namespace Pharmatechnik.Nav.Language.Mcp.Tools;

/// <summary>
/// Ergebnis von <c>nav_grammar</c>: die zur Compile-Zeit aus den <c>Parse*</c>-EBNF-Fragmenten des
/// handgeschriebenen Parsers zusammengesetzte Grammatik der Nav-Sprache (<see cref="NavGrammar"/>) —
/// entweder die vollständige Grammatik oder eine einzelne Produktion, optional ergänzt um die
/// Terminal-Tabelle aus <see cref="SyntaxFacts"/>.
/// </summary>
public sealed class NavGrammarResult {

    /// <summary>Gesetzt, wenn eine einzelne Produktion abgefragt wurde (der angefragte Regelname).</summary>
    public string? Rule { get; set; }

    /// <summary>Die EBNF: die gesamte Grammatik bzw. die einzelne Produktion. Leer im Fehlerfall.</summary>
    public string Ebnf { get; set; } = "";

    /// <summary>Gesetzt, wenn eine unbekannte Regel angefragt wurde (dann ist <see cref="AvailableRules"/> befüllt).</summary>
    public string? Error { get; set; }

    /// <summary>Im Fehlerfall: die bekannten Regelnamen (linke Seiten der Produktionen).</summary>
    public List<string>? AvailableRules { get; set; }

    /// <summary>Optionale Terminal-Tabelle (nur gesetzt, wenn angefordert).</summary>
    public NavGrammarTerminals? Terminals { get; set; }

    public static NavGrammarResult Full(bool includeTerminals) => new() {
        Ebnf      = NavGrammar.Ebnf,
        Terminals = includeTerminals ? NavGrammarTerminals.FromSyntaxFacts() : null
    };

    public static NavGrammarResult Single(string rule, string ebnf, bool includeTerminals) => new() {
        Rule      = rule,
        Ebnf      = ebnf,
        Terminals = includeTerminals ? NavGrammarTerminals.FromSyntaxFacts() : null
    };

    public static NavGrammarResult UnknownRule(string rule) => new() {
        Rule           = rule,
        Error          = $"Unbekannte Regel '{rule}'. Hinweis: Nebenproduktionen (z.B. arrayType) haben "  +
                         "keinen eigenen Schlüssel, sondern stecken im Fragment ihrer Hauptregel (codeType) — "  +
                         "ohne 'rule' nachsehen.",
        AvailableRules = NavGrammar.Rules.Keys.OrderBy(k => k, System.StringComparer.Ordinal).ToList()
    };

}

/// <summary>
/// Die Terminale der Nav-Grammatik, gespiegelt aus <see cref="SyntaxFacts"/>: Schlüsselwörter,
/// Interpunktion und die kategorischen Terminale (Identifier, StringLiteral, EOF, <c>?</c>).
/// </summary>
public sealed class NavGrammarTerminals {

    /// <summary>Die Schlüsselwort-Literale (Nav- und Code-Keywords sowie Kanten-Operatoren).</summary>
    public List<string> Keywords { get; set; } = new();

    /// <summary>Die Interpunktions-Literale, inklusive des Fragezeichens (<c>?</c>).</summary>
    public List<string> Punctuations { get; set; } = new();

    /// <summary>Die kategorischen (nicht-literalen) Terminale.</summary>
    public List<string> Categorical { get; set; } = new() { "Identifier", "StringLiteral", "EOF" };

    public static NavGrammarTerminals FromSyntaxFacts() => new() {
        Keywords     = SyntaxFacts.Keywords.OrderBy(k => k, System.StringComparer.Ordinal).ToList(),
        // "?" (SyntaxTokenType.Questionmark) ist kein Eintrag in SyntaxFacts.Punctuations — gesondert ergänzen.
        Punctuations = SyntaxFacts.Punctuations.Select(c => c.ToString())
                                  .Concat(new[] { "?" })
                                  .OrderBy(p => p, System.StringComparer.Ordinal)
                                  .ToList()
    };

}
