#region Using Directives

using System;
using System.IO;
using System.Linq;

using Pharmatechnik.Nav.Language.Generator;
using Pharmatechnik.Nav.Utilities.IO;
using Pharmatechnik.Nav.Language.Logging;

#endregion

namespace Pharmatechnik.Nav.Language.Analyzer; 

sealed class SyntaxAnalyzerProgram {

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