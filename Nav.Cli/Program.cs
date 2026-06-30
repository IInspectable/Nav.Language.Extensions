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

static class Program  {

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