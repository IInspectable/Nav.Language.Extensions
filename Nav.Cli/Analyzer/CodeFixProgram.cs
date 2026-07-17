using System.Diagnostics;
using System.IO;
using System.Linq;

using Pharmatechnik.Nav.Language.CodeFixes.StyleFix;
using Pharmatechnik.Nav.Language.Generator;
using Pharmatechnik.Nav.Language.Logging;
using Pharmatechnik.Nav.Utilities.IO;

namespace Pharmatechnik.Nav.Language.Analyzer;

/// <summary>
/// Der CodeFix-Nebenpfad des CLI-Hosts: sammelt die <c>.nav</c>-Dateien unter
/// <see cref="CommandLine.Directory"/> ein und lässt die <see cref="CodeFixPipeline"/> darüber laufen, die
/// über <see cref="RemoveUnusedIncludeDirectiveCodeFixProvider.SuggestCodeFixes"/> ungenutzte
/// Include-Direktiven entfernt. Vor dem Schreiben wird jede Datei per <see cref="Checkout"/> (TFS)
/// freigegeben.
/// </summary>
sealed class CodeFixProgram {

    /// <summary>
    /// Führt den CodeFix-Lauf aus: <see cref="ISyntaxProviderFactory"/> je nach
    /// <see cref="CommandLine.UseSyntaxCache"/> wählen, <c>.nav</c>-Dateien exakt filtern (per
    /// <see cref="NavSolution.HasNavExtension"/>) und die Pipeline mit <see cref="Checkout"/> und dem
    /// Include-CodeFix fahren.
    /// </summary>
    /// <param name="cl">Das geparste Options-Modell.</param>
    /// <returns>Stets <c>0</c>.</returns>
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

    /// <summary>
    /// Gibt eine Datei über den TFS-Kommandozeilenclient (<c>TF.exe checkout</c>) zur Bearbeitung frei.
    /// Der Pfad zu <c>TF.exe</c> ist fest auf die Visual-Studio-2022-Enterprise-Installation verdrahtet.
    /// </summary>
    /// <param name="file">Die freizugebende Datei.</param>
    /// <returns><see langword="true"/>, wenn der Checkout-Prozess mit Exit-Code <c>0</c> endete.</returns>
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