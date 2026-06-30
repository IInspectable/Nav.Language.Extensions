#region Using Directives

using System;
using System.Linq;

using NDesk.Options;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Subcommand <c>nav grammar</c>: druckt die EBNF-Grammatik der Nav-Sprache (<see cref="NavGrammar"/>)
/// nach stdout — die gesamte Grammatik oder, mit <c>--rule</c>, eine einzelne Produktion. Die Grammatik
/// wird zur Compile-Zeit aus den <c>Parse*</c>-EBNF-Fragmenten des handgeschriebenen Parsers
/// zusammengesetzt und passt daher stets zum Parser.
/// </summary>
static class GrammarCommand {

    public static int Run(string[] args) {

        string rule     = null;
        bool   showHelp = false;

        var p = new OptionSet {
            { "rule=" , "Druckt nur die Produktion mit diesem Nichtterminal-Namen (linke Seite), z.B. 'taskDefinition'.", v => rule = v },
            { "h|?|help", "Zeigt diese Hilfe an.", v => showHelp = v != null },
        };

        try {
            p.Parse(args);
        } catch (OptionException e) {
            Console.Error.WriteLine("nav.exe grammar: ");
            Console.Error.WriteLine(e.Message);
            Console.Error.WriteLine("Try 'nav.exe grammar --help' for more information.");
            return -1;
        }

        if (showHelp) {
            ShowHelp(p);
            return 0;
        }

        if (string.IsNullOrWhiteSpace(rule)) {
            Console.Out.WriteLine(NavGrammar.Ebnf);
            return 0;
        }

        if (NavGrammar.Rules.TryGetValue(rule, out var ebnf)) {
            Console.Out.WriteLine(ebnf);
            return 0;
        }

        // Nebenproduktionen (z.B. arrayType) haben keinen eigenen Schlüssel, sondern stecken im
        // Fragment ihrer Hauptregel (codeType) — die bekannten Schlüssel zur Orientierung ausgeben.
        Console.Error.WriteLine($"Unbekannte Regel '{rule}'.");
        Console.Error.WriteLine("Bekannte Regeln:");
        foreach (var name in NavGrammar.Rules.Keys.OrderBy(k => k, StringComparer.Ordinal)) {
            Console.Error.WriteLine($"  {name}");
        }

        return -1;
    }

    static void ShowHelp(OptionSet p) {
        Console.WriteLine($"{MyAssembly.ProductName} v{MyAssembly.ProductVersion}");
        Console.WriteLine();
        Console.WriteLine("Usage: nav.exe grammar [OPTIONS]+");
        Console.WriteLine();
        Console.WriteLine("Druckt die EBNF-Grammatik der Nav-Sprache nach stdout.");
        Console.WriteLine();
        Console.WriteLine("Options:");
        p.WriteOptionDescriptions(Console.Out);
    }

}
