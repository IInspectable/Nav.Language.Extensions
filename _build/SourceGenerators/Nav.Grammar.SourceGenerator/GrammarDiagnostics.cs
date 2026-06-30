using Microsoft.CodeAnalysis;

namespace Pharmatechnik.Nav.Language.Grammar.SourceGenerator;

/// <summary>
/// Diagnose-Deskriptoren des Grammatik-Generators. Beide sind Fehler (Build bricht), damit ein
/// Auseinanderdriften von Parser und dokumentierter Grammatik sofort auffällt.
/// </summary>
static class GrammarDiagnostics {

    public const string Category = "NavGrammar";

    /// <summary>NAV001 — ein <c>NavParser.Rule</c>-Wert ohne definierte Produktion.</summary>
    public static readonly DiagnosticDescriptor MissingFragment = new(
        id: "NAV001",
        title: "Grammatikregel ohne EBNF-Fragment",
        messageFormat: "Die Grammatikregel '{0}' (NavParser.Rule) hat kein EBNF-Fragment im Doku-Kommentar der zugehörigen Parse*-Methode",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Jeder Wert des NavParser.Rule-Enums muss eine über ein ::=-EBNF-Fragment definierte Produktion besitzen.");

    /// <summary>NAV002 — ein referenziertes Nichtterminal ohne eigene Produktion (Grammatik nicht geschlossen).</summary>
    public static readonly DiagnosticDescriptor UndefinedNonterminal = new(
        id: "NAV002",
        title: "Grammatik nicht geschlossen",
        messageFormat: "Das Nichtterminal '{0}' wird in der Regel '{1}' referenziert, ist aber nirgends definiert",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Jedes auf einer rechten Regelseite referenzierte Nichtterminal muss eine eigene Produktion besitzen (geschlossene Grammatik).");

}
