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

    /// <summary>
    /// Umhüllt den optionalen <see cref="ILogger"/> des Hosts für die Dauer eines
    /// <see cref="Run"/>-Laufs und bündelt die Protokoll-Logik der Pipeline: Zeitmessung (Gesamt- und
    /// je Datei), Unterdrückung doppelt gemeldeter Diagnosen sowie das Merken, ob überhaupt schon ein
    /// Fehler auftrat (<see cref="HasLoggedErrors"/>, steuert den <see cref="RunResult"/>). Ist kein
    /// Logger gesetzt, entfallen die eigentlichen Ausgaben, die Buchführung (Fehler-/Zeit-Status) läuft
    /// aber weiter.
    /// </summary>
    sealed class LoggerAdapter: IDisposable {

        readonly ILogger? _logger;

        readonly HashSet<Diagnostic> _loggedErrors;
        readonly HashSet<Diagnostic> _loggedWarnings;
        readonly Stopwatch           _processStopwatch;
        readonly Stopwatch           _processFileStopwatch;

        /// <summary>Erzeugt den Adapter über den optionalen Host-Logger.</summary>
        /// <param name="logger">Die Ausgabesenke oder <see langword="null"/>.</param>
        public LoggerAdapter(ILogger? logger) {
            _logger               = logger;
            _loggedErrors         = new HashSet<Diagnostic>();
            _loggedWarnings       = new HashSet<Diagnostic>();
            _processStopwatch     = new Stopwatch();
            _processFileStopwatch = new Stopwatch();
        }

        /// <summary><see langword="true"/>, sobald während des Laufs mindestens ein Fehler gemeldet
        /// wurde. Bestimmt am Ende, ob <see cref="Run"/> ein <see cref="RunResult.Failed"/>
        /// zurückgibt.</summary>
        public bool HasLoggedErrors { get; private set; }

        /// <summary>Meldet einen Freitext-Fehler und setzt <see cref="HasLoggedErrors"/>.</summary>
        /// <param name="message">Der Fehlertext.</param>
        public void LogError(string message) {
            HasLoggedErrors = true;
            _logger?.LogError(message);
        }

        /// <summary>
        /// Meldet die Fehler-Diagnosen aus <paramref name="diagnostics"/> (jede nur einmal je Lauf) und
        /// setzt bei mindestens einem Fehler <see cref="HasLoggedErrors"/>.
        /// </summary>
        /// <param name="diagnostics">Die zu prüfenden Diagnosen; nur die mit Fehler-Schweregrad werden
        /// gemeldet.</param>
        /// <returns><see langword="true"/>, wenn mindestens eine Fehler-Diagnose enthalten war — der
        /// Aufrufer überspringt dann die betroffene Datei.</returns>
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

        /// <summary>Meldet die Warn-Diagnosen aus <paramref name="diagnostics"/> (jede nur einmal je
        /// Lauf).</summary>
        /// <param name="diagnostics">Die zu prüfenden Diagnosen; nur die mit Warn-Schweregrad werden
        /// gemeldet.</param>
        public void LogWarnings(IEnumerable<Diagnostic> diagnostics) {
            foreach (var warning in diagnostics.Warnings()) {
                if (_loggedWarnings.Add(warning)) {
                    _logger?.LogWarning(warning);
                }
            }
        }

        /// <summary>Markiert den Beginn des Gesamtlaufs und startet die Gesamt-Zeitmessung.</summary>
        public void LogProcessBegin() {

            _processStopwatch.Restart();
        }

        /// <summary>Markiert den Beginn der Verarbeitung einer Datei, startet die Je-Datei-Zeitmessung
        /// und meldet dies als Verbose-Ausgabe.</summary>
        /// <param name="fileSpec">Die gerade verarbeitete Eingabedatei.</param>
        public void LogProcessFileBegin(FileSpec fileSpec) {
            _processFileStopwatch.Restart();
            _logger?.LogVerbose($"Processing file '{fileSpec.Identity}'");
        }

        /// <summary>
        /// Meldet je erzeugter Ausgabedatei eine Verbose-Zeile: <c>+</c> für geschriebene
        /// (<see cref="FileGeneratorAction.Updated"/>), <c>~</c> für inhaltsgleich übersprungene
        /// Dateien. Der Dateiname wird — soweit möglich — relativ zum Verzeichnis der zugrunde
        /// liegenden <c>.nav</c>-Quelle dargestellt.
        /// </summary>
        /// <param name="fileResults">Die Ergebnisse der Dateiausgabe für eine Task-Definition.</param>
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

        /// <summary>Markiert das Ende der Verarbeitung einer Datei, stoppt die Je-Datei-Zeitmessung und
        /// meldet die verstrichene Zeit als Verbose-Ausgabe.</summary>
        /// <param name="fileSpec">Die abgeschlossene Eingabedatei (nur zur Symmetrie mit
        /// <see cref="LogProcessFileBegin"/>; der Name geht nicht in die Ausgabe ein).</param>
        // ReSharper disable once UnusedParameter.Local
        public void LogProcessFileEnd(FileSpec fileSpec) {
            _processFileStopwatch.Stop();
            _logger?.LogVerbose($"Completed in {_processFileStopwatch.Elapsed.TotalSeconds} seconds.");
        }

        /// <summary>
        /// Markiert das Ende des Gesamtlaufs, stoppt die Gesamt-Zeitmessung und gibt eine
        /// zusammenfassende Info-Ausgabe aus (Produktname/-version, die aus
        /// <paramref name="statistic"/> gezogenen Zähler sowie die Gesamtdauer), umrahmt von je einer
        /// horizontalen Linie.
        /// </summary>
        /// <param name="statistic">Die während des Laufs geführte Statistik.</param>
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

        /// <summary>Zentriert <paramref name="message"/> zwischen zwei aus <paramref name="lineChar"/>
        /// gebildeten Linien, sodass die Gesamtbreite <paramref name="length"/> ergibt.</summary>
        /// <param name="message">Der einzurahmende Text.</param>
        /// <param name="length">Die angestrebte Gesamtbreite der Zeile.</param>
        /// <param name="lineChar">Das für die Linien verwendete Zeichen (Vorgabe: <c>-</c>).</param>
        /// <returns>Die eingerahmte Zeile.</returns>
        static string HorizontalRule(string message, int length, char lineChar = '-') {

            length -= 2; // Leerzeichen zwischen den Linien

            var padLeft  = Math.Max(0, (length - message.Length) / 2);
            var padRight = Math.Max(0, length - message.Length - padLeft);

            return $"{new String(lineChar, padLeft)} {message} {new String(lineChar, padRight)}";
        }

        /// <summary>Liefert <paramref name="word"/> unverändert für <paramref name="count"/> == 1 und
        /// andernfalls die einfache Plural-Form (<c>+ "s"</c>) für die englischsprachige
        /// Statistikausgabe.</summary>
        /// <param name="word">Das zu beugende Wort.</param>
        /// <param name="count">Die zugehörige Anzahl.</param>
        /// <returns>Singular- oder Plural-Form.</returns>
        string Pluralize(string word, int count) {
            if (count == 1) {
                return word;
            }

            return $"{word}s";
        }

        /// <summary>Aktuell ohne Freigabe-Aufwand; erfüllt nur den <see cref="IDisposable"/>-Vertrag,
        /// damit der Adapter im <c>using</c> von <see cref="Run"/> verwendet werden kann.</summary>
        public void Dispose() {
        }

    }

}