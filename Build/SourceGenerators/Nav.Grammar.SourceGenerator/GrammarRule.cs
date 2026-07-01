using Microsoft.CodeAnalysis;

namespace Pharmatechnik.Nav.Language.Grammar.SourceGenerator;

/// <summary>
/// Eine einzelne Grammatik-Produktion, extrahiert aus dem EBNF-Fragment einer <c>Parse*</c>-Methode.
/// Bewusst rein wertbasiert (Strings/int/<see cref="Microsoft.CodeAnalysis.Location"/>) — kein
/// <c>ISymbol</c> —, damit das inkrementelle Caching der Generator-Pipeline greift.
/// </summary>
/// <param name="RuleName">Der Name des Nichtterminals (linke Seite vor <c>::=</c>).</param>
/// <param name="Ebnf">Das vollständige EBNF-Fragment (linke und rechte Seite).</param>
/// <param name="Order">Sortierschlüssel = Position der Methode in der Quelldatei (Lesereihenfolge).</param>
/// <param name="Location">Quellort der Parse*-Methode (für Diagnosen).</param>
sealed record GrammarRule(string RuleName, string Ebnf, int Order, Location Location);

/// <summary>
/// Ein Wert des <c>NavParser.Rule</c>-Enums — die Registry der individuell parsbaren Produktionen.
/// </summary>
/// <param name="Name">Der Enum-Member-Name (z.B. <c>TaskDefinition</c>).</param>
/// <param name="Location">Quellort des Enum-Members (für Diagnosen).</param>
sealed record RuleEnumMember(string Name, Location Location);
