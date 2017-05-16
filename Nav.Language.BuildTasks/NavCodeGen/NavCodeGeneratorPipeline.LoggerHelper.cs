﻿#region Using Directives

using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using JetBrains.Annotations;

using Pharmatechnik.Nav.Utilities.IO;
using Pharmatechnik.Nav.Language.CodeGen;

#endregion

namespace Pharmatechnik.Nav.Language.BuildTasks {

    public sealed partial class NavCodeGeneratorPipeline {
        public sealed class LoggerHelper {

            public LoggerHelper(IGeneratorLogger logger) {
                Logger               = logger;
                ProcessStopwatch     = new Stopwatch();
                ProcessFileStopwatch = new Stopwatch();
            }

            [CanBeNull]
            IGeneratorLogger Logger { get; }
            [NotNull]
            Stopwatch ProcessStopwatch { get; }
            [NotNull]
            Stopwatch ProcessFileStopwatch { get; }

            public bool HasLoggedErrors { get; set; }

            public void LogInfo(string message) {
                Logger?.LogInfo(message);
            }

            public void LogError(string message) {
                HasLoggedErrors = true;
                Logger?.LogError(message);
            }

            public bool LogErrors(FileSpec fileSpec, IEnumerable<Diagnostic> diagnostics) {

                bool errorsLogged = false;
                foreach (var error in diagnostics.Errors()) {
                    errorsLogged = true;
                    HasLoggedErrors = true;
                    Logger?.LogError(error, fileSpec);
                }
                return errorsLogged;
            }

            public void LogWarnings(FileSpec fileSpec, IEnumerable<Diagnostic> diagnostics) {
                foreach (var warning in diagnostics.Warnings()) {
                    Logger?.LogWarning(warning, fileSpec);
                }
            }

            public void LogProcessBegin() {
                ProcessStopwatch.Restart();
            }

            public void LogProcessFileBegin(FileSpec fileSpec) {
                ProcessFileStopwatch.Restart();
                Logger?.LogInfo($"Processing file '{fileSpec.Identity}'");
            }

            public void LogProcessFileEnd(FileSpec fileSpec) {
                ProcessFileStopwatch.Stop();
                Logger?.LogInfo($"Completed in {ProcessFileStopwatch.Elapsed}");
            }

            public void LogProcessEnd(int fileCount) {
                ProcessStopwatch.Stop();
                Logger?.LogInfo($"{fileCount} Files completed in {ProcessStopwatch.Elapsed}");
            }

            public void LogFileGeneratorResults(IImmutableList<FileGeneratorResult> fileResults) {

                var longestName = fileResults.Select(r => r.Action.ToString().Length).Max();

                foreach (var fileResult in fileResults) {

                    var fileIdentity = fileResult.FileName;

                    var syntaxDirectory = fileResult.TaskDefinition.Syntax.SyntaxTree.FileInfo?.DirectoryName;
                    if (syntaxDirectory != null) {
                        fileIdentity = PathHelper.GetRelativePath(syntaxDirectory, fileResult.FileName);
                    }

                    var action  = fileResult.Action.ToString().PadRight(longestName);
                    var message = $"   {action}: {fileIdentity}";
                    Logger?.LogInfo(message);
                }
            }

            
        }
    }
}