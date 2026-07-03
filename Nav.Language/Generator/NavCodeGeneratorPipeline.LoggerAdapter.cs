#region Using Directives

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

using Pharmatechnik.Nav.Language.CodeGen;
using Pharmatechnik.Nav.Utilities.IO;

#endregion

namespace Pharmatechnik.Nav.Language.Generator;

public sealed partial class NavCodeGeneratorPipeline {

    sealed class LoggerAdapter: IDisposable {

        readonly ILogger? _logger;

        readonly HashSet<Diagnostic> _loggedErrors;
        readonly HashSet<Diagnostic> _loggedWarnings;
        readonly Stopwatch           _processStopwatch;
        readonly Stopwatch           _processFileStopwatch;

        public LoggerAdapter(ILogger? logger) {
            _logger               = logger;
            _loggedErrors         = new HashSet<Diagnostic>();
            _loggedWarnings       = new HashSet<Diagnostic>();
            _processStopwatch     = new Stopwatch();
            _processFileStopwatch = new Stopwatch();
        }

        public bool HasLoggedErrors { get; private set; }

        public void LogError(string message) {
            HasLoggedErrors = true;
            _logger?.LogError(message);
        }

        public bool LogErrors(IEnumerable<Diagnostic> diagnostics) {

            bool errorsLogged = false;
            foreach (var error in diagnostics.Errors()) {

                if (_loggedErrors.Add(error)) {
                    _logger?.LogError(error);
                }

                errorsLogged    = true;
                HasLoggedErrors = true;
            }

            return errorsLogged;
        }

        public void LogWarnings(IEnumerable<Diagnostic> diagnostics) {
            foreach (var warning in diagnostics.Warnings()) {
                if (_loggedWarnings.Add(warning)) {
                    _logger?.LogWarning(warning);
                }
            }
        }

        public void LogProcessBegin() {

            _processStopwatch.Restart();
        }

        public void LogProcessFileBegin(FileSpec fileSpec) {
            _processFileStopwatch.Restart();
            _logger?.LogVerbose($"Processing file '{fileSpec.Identity}'");
        }

        public void LogFileGeneratorResults(IImmutableList<FileGeneratorResult> fileResults) {

            foreach (var fileResult in fileResults) {

                var fileIdentity = fileResult.FileName;

                var syntaxDirectory = fileResult.TaskDefinition.Syntax.SyntaxTree.SourceText.FileInfo?.DirectoryName;
                if (syntaxDirectory != null) {
                    fileIdentity = PathHelper.GetRelativePath(syntaxDirectory, fileResult.FileName);
                }

                if (fileResult.Action == FileGeneratorAction.Updated) {
                    var message = $"   + {fileIdentity}";
                    _logger?.LogVerbose(message);
                } else {
                    var message = $"   ~ {fileIdentity}";
                    _logger?.LogVerbose(message);
                }
            }
        }

        // ReSharper disable once UnusedParameter.Local
        public void LogProcessFileEnd(FileSpec fileSpec) {
            _processFileStopwatch.Stop();
            _logger?.LogVerbose($"Completed in {_processFileStopwatch.Elapsed.TotalSeconds} seconds.");
        }

        public void LogProcessEnd(Statistic statistic) {
            _processStopwatch.Stop();

            var lines = new[] {
                $"{MyAssembly.ProductName}, Version {MyAssembly.ProductVersion}",
                $"{statistic.FileCount} .nav {Pluralize("file",                statistic.FileCount)} with {statistic.TaskCount} task {Pluralize("definition", statistic.TaskCount)} processed.",
                $"   Updated: {statistic.FilesUpated,3} .cs {Pluralize("file", statistic.FilesUpated)}",
                $"   Skipped: {statistic.FilesSkiped,3} .cs {Pluralize("file", statistic.FilesSkiped)}",
                $"Completed in {_processStopwatch.Elapsed.TotalSeconds} seconds"
            };

            var hrWidth = lines.Max(line => line.Length);
            hrWidth += hrWidth % 2; // Auf gerade Zahl aufrunden

            for (int i = 0; i < lines.Length; i++) {
                var line = lines[i];

                // Die erste und letzte Zeile bekommen einen horizontalen Strich
                if (i == 0 || i == lines.Length - 1) {
                    _logger?.LogInfo(HorizontalRule(line, hrWidth));
                } else {
                    _logger?.LogInfo(line);
                }
            }
        }

        static string HorizontalRule(string message, int length, char lineChar = '-') {

            length -= 2; // Leerzeichen zwischen den Linien

            var padLeft  = Math.Max(0, (length - message.Length) / 2);
            var padRight = Math.Max(0, length - message.Length - padLeft);

            return $"{new String(lineChar, padLeft)} {message} {new String(lineChar, padRight)}";
        }

        string Pluralize(string word, int count) {
            if (count == 1) {
                return word;
            }

            return $"{word}s";
        }

        public void Dispose() {
        }

    }

}