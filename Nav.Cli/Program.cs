#region Using Directives

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;

using Pharmatechnik.Nav.Language.Analyzer;
using Pharmatechnik.Nav.Language.Generator;

#endregion

namespace Pharmatechnik.Nav.Language; 

/// <summary>
/// Der Einstiegspunkt des CLI-Hosts (<c>nav.exe</c>). <see cref="Main"/> löst zunächst ein etwaiges
/// Response-File (<c>@datei</c>) auf, bedient dann das Subcommand <c>nav grammar</c>
/// (<see cref="GrammarCommand"/>) und parst andernfalls die Kommandozeile zu einem
/// <see cref="CommandLine"/>. Aus dessen <see cref="CommandLine.Analyze"/>-Weiche ergibt sich, ob der
/// Analyse-Pfad (<see cref="SyntaxAnalyzerProgram"/>) oder der Standard-Codegenerator
/// (<see cref="NavCodeGenerator"/>) läuft.
/// </summary>
static class Program  {

    /// <summary>
    /// Der Prozess-Einstiegspunkt. Reihenfolge: UTF-8-Konsolenkodierung setzen, Response-File auflösen,
    /// <c>grammar</c>-Subcommand abfangen, Kommandozeile parsen und je nach <see cref="CommandLine.Analyze"/>
    /// an den Analyzer bzw. Codegenerator delegieren.
    /// </summary>
    /// <param name="args">Die rohen Kommandozeilenargumente. Ein einzelnes Argument der Form <c>@datei</c>
    /// wird als Response-File interpretiert und über <see cref="LoadArgs"/> expandiert.</param>
    /// <returns>Der Prozess-Exit-Code: <c>0</c> bei Erfolg, <c>-1</c> bei ungültiger Kommandozeile, sonst
    /// der Rückgabewert des ausgeführten Pfads.</returns>
    static int Main(string[] args) {

        Console.OutputEncoding = Encoding.UTF8;

        var cmdArgs = args;
            
        // Response file
        if (args.Length == 1 && args[0].StartsWith("@")) {
            var fileName = args[0].Substring(1);
            cmdArgs = LoadArgs(fileName);
        }

        // Subcommand: nav grammar [--rule X] — druckt die EBNF-Grammatik nach stdout.
        if (cmdArgs.Length >= 1 && string.Equals(cmdArgs[0], "grammar", StringComparison.OrdinalIgnoreCase)) {
            return GrammarCommand.Run(cmdArgs.Skip(1).ToArray());
        }

        var cl = CommandLine.Parse(cmdArgs);
        if (cl == null) {
            return -1;
        }
            
        if (cl.Analyze) {
            var p = new SyntaxAnalyzerProgram();
            return p.Run(cl);
        } else {
            var p = new NavCodeGenerator();
            return p.Run(cl);
        }                      
    }

    /// <summary>
    /// Liest die Argumente eines Response-Files zeilenweise ein und zerlegt sie an Leerzeichen in einzelne
    /// Argumente. In einfache oder doppelte Anführungszeichen gefasste Abschnitte werden zusammengehalten,
    /// sodass Argumente mit Leerzeichen (z.B. Pfade) erhalten bleiben.
    /// </summary>
    /// <param name="file">Der Pfad des Response-Files (der Teil nach dem <c>@</c>).</param>
    /// <returns>Die expandierten Argumente.</returns>
    // TODO in Utility Klasse
    static string[] LoadArgs(string file) {

        using var reader = new StreamReader(file);

        var args = new List<string>();
        var sb   = new StringBuilder();

        while (reader.ReadLine() is { } line) {

            int t = line.Length;

            for (int i = 0; i < t; i++) {
                char c = line[i];

                if (c == '"' || c == '\'') {
                    char quoteEnd = c;

                    for (i++; i < t; i++) {
                        c = line[i];

                        if (c == quoteEnd) {
                            break;
                        }
                        sb.Append(c);
                    }
                } else if (c == ' ') {
                    if (sb.Length > 0) {
                        args.Add(sb.ToString());
                        sb.Length = 0;
                    }
                } else {
                    sb.Append(c);
                }
            }
            if (sb.Length > 0) {
                args.Add(sb.ToString());
                sb.Length = 0;
            }
        }

        return args.ToArray();

    }
}