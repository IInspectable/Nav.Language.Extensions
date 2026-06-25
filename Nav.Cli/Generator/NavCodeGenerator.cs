#region Using Directives

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

using Pharmatechnik.Nav.Utilities.IO;
using Pharmatechnik.Nav.Language.CodeGen;
using Pharmatechnik.Nav.Language.Logging;
using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.Generator;

class NavCodeGenerator {

    public int Run(CommandLine cl) {

        var logger = new ConsoleLogger(
            fullPaths : cl.FullPaths,
            noWarnings: cl.NoWarnings,
            verbose   : cl.Verbose);

        try {

            var fileSpecs = CollectFiles(cl, logger);
            var pipeline  = CreatePipeline(cl, logger);

            return pipeline.Run(fileSpecs) ? 0 : 1;

        } catch (Exception ex) {

            logger.LogError(ex.ToString());

            return -1;
        }
    }

    static NavCodeGeneratorPipeline CreatePipeline(CommandLine cl, ConsoleLogger logger) {

        var syntaxProviderFactory = cl.UseSyntaxCache ? SyntaxProviderFactory.Cached : SyntaxProviderFactory.Default;

        var options = new GenerationOptions {
            Force                = cl.Force,
            Strict               = cl.Strict,
            GenerateToClasses    = (cl.GenerationOptions & CodeGenerationOptions.ToClasses)   != 0,
            GenerateWflClasses   = (cl.GenerationOptions & CodeGenerationOptions.WflClasses)  != 0,
            GenerateIwflClasses  = (cl.GenerationOptions & CodeGenerationOptions.IwflClasses) != 0,
            ProjectRootDirectory = cl.ProjectRootDirectory,
            IwflRootDirectory    = cl.IwflRootDirectory,
            WflRootDirectory     = cl.WflRootDirectory,

        };

        ValidateOptions();

        var pipeline = NavCodeGeneratorPipeline.Create(options: options, logger: logger, syntaxProviderFactory: syntaxProviderFactory);

        return pipeline;

        void ValidateOptions() {

            if (!options.ProjectRootDirectory.IsNullOrEmpty() &&
                !Directory.Exists(options.ProjectRootDirectory)) {
                throw new ArgumentException($"Das Project Wurzelverzeichnis '{options.ProjectRootDirectory}' exisitiert nicht.");
            }

            if (!options.WflRootDirectory.IsNullOrEmpty() &&
                options.ProjectRootDirectory.IsNullOrEmpty()
               ) {
                throw new ArgumentException($"es wurde ein alternatives WFL Wurzelverzeichnis '{options.IwflRootDirectory}' angegeben, aber kein Project Wurzelverzeichnis.");
            }

            if (!options.IwflRootDirectory.IsNullOrEmpty() &&
                options.ProjectRootDirectory.IsNullOrEmpty()
               ) {
                throw new ArgumentException($"es wurde ein alternatives IWFL Wurzelverzeichnis '{options.IwflRootDirectory}' angegeben, aber kein Project Wurzelverzeichnis.");
            }
        }
    }

    static IEnumerable<FileSpec> CollectFiles(CommandLine cl, ConsoleLogger logger) {

        var result = new List<FileSpec>();

        // /d:-Modus: Das Verzeichnis ist zugleich die .navignore-Scangrenze.
        if (cl.Directory != null) {

            var ignore = NavIgnore.Load(cl.Directory);

            foreach (var file in Directory.EnumerateFiles(cl.Directory, "*.nav", SearchOption.AllDirectories)) {

                if (ignore.IsIgnored(file)) {
                    logger.LogVerbose($"Übersprungen (.navignore): {file}");
                    continue;
                }

                result.Add(new FileSpec(identity: PathHelper.GetRelativePath(cl.Directory, file), fileName: file));
            }
        }

        // /s:-Modus: Einzeldateien ohne gemeinsame Wurzel — pro Datei die .navignore-Vorfahren auswerten.
        if (cl.Sources != null) {

            var ignoreByDir = new Dictionary<string, NavIgnore>(StringComparer.OrdinalIgnoreCase);

            foreach (var source in cl.Sources) {

                var fullPath = PathHelper.GetFullPathNoThrow(source);
                var dir      = Path.GetDirectoryName(fullPath) ?? fullPath;

                if (!ignoreByDir.TryGetValue(dir, out var ignore)) {
                    ignore           = NavIgnore.LoadForAncestors(dir);
                    ignoreByDir[dir] = ignore;
                }

                if (ignore.IsIgnored(fullPath)) {
                    logger.LogInfo($"Übersprungen (.navignore): {source}");
                    continue;
                }

                result.Add(FileSpec.FromFile(source));
            }
        }

        return result;
    }

}