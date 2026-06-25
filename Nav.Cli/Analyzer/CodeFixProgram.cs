using System.Diagnostics;
using System.IO;
using System.Linq;

using Pharmatechnik.Nav.Language.CodeFixes.StyleFix;
using Pharmatechnik.Nav.Language.Generator;
using Pharmatechnik.Nav.Language.Logging;
using Pharmatechnik.Nav.Utilities.IO;

namespace Pharmatechnik.Nav.Language.Analyzer;

sealed class CodeFixProgram {

    public int Run(CommandLine cl) {

        var syntaxProviderFactory = cl.UseSyntaxCache ? SyntaxProviderFactory.Cached : SyntaxProviderFactory.Default;

        var logger   = new ConsoleLogger(fullPaths: cl.FullPaths, noWarnings: cl.NoWarnings, verbose: cl.Verbose);
        var pipeline = new CodeFixPipeline(logger, syntaxProviderFactory);

        var navFiles  = Directory.EnumerateFiles(cl.Directory, NavSolution.SearchFilter, SearchOption.AllDirectories)
                                 .Where(NavSolution.HasNavExtension); // *.nav matcht unter Windows auch .navignore & Co.
        var fileSpecs = navFiles.Select(file => new FileSpec(identity: PathHelper.GetRelativePath(cl.Directory, file), fileName: file));

        pipeline.Run(fileSpecs, Checkout, RemoveUnusedIncludeDirectiveCodeFixProvider.SuggestCodeFixes);

        return 0;
    }

    static bool Checkout(string file) {

        var psi = new ProcessStartInfo("C:\\Program Files\\Microsoft Visual Studio\\2022\\Enterprise\\Common7\\IDE\\CommonExtensions\\Microsoft\\TeamFoundation\\Team Explorer\\TF.exe") {
            Arguments       = $"checkout \"{file}\"",
            CreateNoWindow  = true,
            UseShellExecute = false
        };

        var process = Process.Start(psi);

        process?.WaitForExit();

        return process?.ExitCode == 0;
    }

}