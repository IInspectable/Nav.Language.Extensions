#region Using Directives

using System;
using System.IO;
using System.Linq;

using Pharmatechnik.Nav.Language.Generator;
using Pharmatechnik.Nav.Utilities.IO;
using Pharmatechnik.Nav.Language.Logging;

#endregion

namespace Pharmatechnik.Nav.Language.Analyzer; 

/// <summary>
/// Der Analyse-Nebenpfad des CLI-Hosts (aktiv bei <see cref="CommandLine.Analyze"/>): sammelt die
/// <c>.nav</c>-Dateien unter <see cref="CommandLine.Directory"/> ein und lässt einen
/// <see cref="CodeNotImplementedAnalyzer"/> (mit <see cref="CommandLine.Pattern"/> als Suchmuster) über die
/// <see cref="SyntaxAnalyzerPipeline"/> darüber laufen; am Ende wird die Trefferzahl ausgegeben.
/// </summary>
sealed class SyntaxAnalyzerProgram {

    /// <summary>
    /// Führt den Analyselauf aus: <see cref="ISyntaxProviderFactory"/> je nach
    /// <see cref="CommandLine.UseSyntaxCache"/> wählen, <c>.nav</c>-Dateien exakt filtern (per
    /// <see cref="NavSolution.HasNavExtension"/>, da <c>*.nav</c> unter Windows auch <c>.navignore</c> &amp; Co.
    /// matcht), Pipeline fahren und die Anzahl der Treffer ausgeben.
    /// </summary>
    /// <param name="cl">Das geparste Options-Modell.</param>
    /// <returns>Stets <c>0</c>.</returns>
    public int Run(CommandLine cl) {

        var syntaxProviderFactory = cl.UseSyntaxCache ? SyntaxProviderFactory.Cached : SyntaxProviderFactory.Default;

        var logger   = new ConsoleLogger(fullPaths: cl.FullPaths, noWarnings: cl.NoWarnings, verbose: cl.Verbose);
        var pipeline = new SyntaxAnalyzerPipeline(logger, syntaxProviderFactory);

        var navFiles  = Directory.EnumerateFiles(cl.Directory, NavSolution.SearchFilter, SearchOption.AllDirectories)
                                 .Where(NavSolution.HasNavExtension); // *.nav matcht unter Windows auch .navignore & Co.
        var fileSpecs = navFiles.Select(file => new FileSpec(identity: PathHelper.GetRelativePath(cl.Directory, file), fileName: file));
        var analyzer  = new CodeNotImplementedAnalyzer(cl.Pattern);

        pipeline.Run(fileSpecs, analyzer);

        Console.WriteLine($"Number of CodeNotImplemented: {analyzer.Result}");

        return 0;
    }

}