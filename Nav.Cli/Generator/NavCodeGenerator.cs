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

            var result = pipeline.Run(fileSpecs);

            // Bei gesetztem Manifest-Pfad alle erzeugten Ausgabedateien (auch inhaltsgleich
            // übersprungene) für den inkrementellen MSBuild-Build protokollieren. Nur bei Erfolg —
            // sonst bliebe das (alte) Manifest älter als die Inputs und der nächste Build generiert
            // erneut.
            if (result.Succeeded && !cl.ManifestFile.IsNullOrEmpty()) {
                WriteManifest(cl.ManifestFile, result.GeneratedFiles.Select(r => r.FileName));
            }

            // Abhängigkeits-Manifest: die per taskref eingelesenen Dateien, die selbst keine
            // Eingabedateien sind. Der inkrementelle Build liest dieses Manifest beim nächsten Lauf und
            // hängt die Pfade als zusätzliche Inputs an — so löst eine Änderung an einer solchen
            // Abhängigkeit korrekt einen Regen aus, statt fälschlich übersprungen zu werden. Nur bei
            // Erfolg schreiben (analog zum Outputs-Manifest).
            if (result.Succeeded && !cl.DependencyManifestFile.IsNullOrEmpty()) {
                WriteManifest(cl.DependencyManifestFile, result.IncludedFiles);
            }

            return result.Succeeded ? 0 : 1;

        } catch (Exception ex) {

            logger.LogError(ex.ToString());

            return -1;
        }
    }

    static void WriteManifest(string manifestFile, IEnumerable<string> generatedFiles) {

        var lines = generatedFiles
                   .Select(PathHelper.GetFullPathNoThrow)
                   .Where(path => !path.IsNullOrEmpty())
                   .Distinct(StringComparer.OrdinalIgnoreCase)
                   .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                   .ToList();

        var directory = Path.GetDirectoryName(manifestFile);
        if (!directory.IsNullOrEmpty()) {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllLines(manifestFile, lines);
    }

    static NavCodeGeneratorPipeline CreatePipeline(CommandLine cl, ConsoleLogger logger) {

        var syntaxProviderFactory = cl.UseSyntaxCache ? SyntaxProviderFactory.Cached : SyntaxProviderFactory.Default;

        var options = new GenerationOptions {
            Force                = cl.Force,
            Strict               = cl.Strict,
            GenerateToClasses    = (cl.GenerationOptions & CodeGenerationOptions.ToClasses)   != 0,
            GenerateWflClasses   = (cl.GenerationOptions & CodeGenerationOptions.WflClasses)  != 0,
            GenerateIwflClasses  = (cl.GenerationOptions & CodeGenerationOptions.IwflClasses) != 0,
            NullableContext      = cl.NullableContext,
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

            foreach (var file in Directory.EnumerateFiles(cl.Directory, NavSolution.SearchFilter, SearchOption.AllDirectories)) {

                // Windows *.nav matcht auch .navignore & Co. (3-Zeichen-Endung) — exakt filtern.
                if (!NavSolution.HasNavExtension(file)) {
                    continue;
                }

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