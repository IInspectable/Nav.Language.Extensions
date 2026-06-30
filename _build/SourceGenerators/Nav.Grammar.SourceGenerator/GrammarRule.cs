namespace Pharmatechnik.Nav.Language.Grammar.SourceGenerator;

/// <summary>
/// Eine einzelne Grammatik-Produktion, extrahiert aus dem EBNF-Fragment einer <c>Parse*</c>-Methode.
/// Bewusst rein wertbasiert (nur Strings/int) — kein <c>ISymbol</c> —, damit das inkrementelle
/// Caching der Generator-Pipeline greift.
/// </summary>
/// <param name="RuleName">Der Name des Nichtterminals (linke Seite vor <c>::=</c>).</param>
/// <param name="Ebnf">Das vollständige EBNF-Fragment (linke und rechte Seite).</param>
/// <param name="Order">Sortierschlüssel = Position der Methode in der Quelldatei (Lesereihenfolge).</param>
sealed record GrammarRule(string RuleName, string Ebnf, int Order);
