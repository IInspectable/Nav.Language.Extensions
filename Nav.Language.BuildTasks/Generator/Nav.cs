#region Using Directives

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

#endregion

namespace Pharmatechnik.Nav.Language.BuildTasks {

    /// <summary>
    /// Der MSBuild-Task, der die <c>.nav</c>-Dateien beim Build zu C#-Code übersetzt, indem er die
    /// <c>nav.exe</c> als externes Werkzeug aufruft. Über <c>&lt;UsingTask&gt;</c> in
    /// <c>Pharmatechnik.Nav.Language.targets</c> deklariert und im <c>GenerateNavCode</c>-Target
    /// instanziiert.
    /// </summary>
    /// <remarks>
    /// Als <see cref="ToolTask"/> reduziert sich die Aufgabe darauf, aus den gesetzten Parametern das
    /// Kommandozeilen-Argument der <c>nav.exe</c> zu bauen (<see cref="GenerateResponseFileCommands"/>),
    /// das Werkzeug zu lokalisieren (<see cref="GenerateFullPathToTool"/>) und dessen
    /// Konsolen-Ausgabe in MSBuild-Meldungen zu übersetzen (<see cref="LogEventsFromTextOutput"/>).
    /// Der net472-Task lädt die net10-<c>nav.exe</c> bewusst nur als externen Prozess, nie als
    /// Assembly. Die eigentliche Sprachlogik liegt vollständig in der Engine/CLI.
    /// </remarks>
    public class Nav: ToolTask {

        /// <summary>Erzwingt die vollständige Neugenerierung (Schalter <c>/f</c>) — ignoriert die inkrementellen Manifeste.</summary>
        public bool Force { get; set; }

        /// <summary>Behandelt Warnungen als Fehler (Schalter <c>/t</c>, „strict").</summary>
        public bool Strict { get; set; }

        /// <summary>Die zu übersetzenden <c>.nav</c>-Eingabedateien (Schalter <c>/s:</c>, je Datei einmal).</summary>
        public ITaskItem[] Sources { get; set; }

        /// <summary>Ob die TO-Klassen erzeugt werden (fließt in den <c>/g:</c>-Schalter als <see cref="CodeGenerationOptions.ToClasses"/> ein).</summary>
        public bool GenerateToClasses { get; set; }

        /// <summary>Ob die WFL-Klassen erzeugt werden (fließt in den <c>/g:</c>-Schalter als <see cref="CodeGenerationOptions.WflClasses"/> ein).</summary>
        public bool GenerateWflClasses { get; set; }

        /// <summary>Ob die IWFL-Klassen erzeugt werden (fließt in den <c>/g:</c>-Schalter als <see cref="CodeGenerationOptions.IwflClasses"/> ein).</summary>
        public bool GenerateIwflClasses { get; set; }

        /// <summary>
        /// Übersetzt die drei <c>Generate…Classes</c>-Schalter in die einzeln zu serialisierenden
        /// <see cref="CodeGenerationOptions"/>-Werte. Ist keiner gesetzt, wird
        /// <see cref="CodeGenerationOptions.None"/> geliefert.
        /// </summary>
        IEnumerable<CodeGenerationOptions> GetCodeGenerationOptions() {

            var options = new (Func<bool> IsOn, CodeGenerationOptions EnumValue)[] {
                (() => GenerateToClasses, CodeGenerationOptions.ToClasses),
                (() => GenerateWflClasses, CodeGenerationOptions.WflClasses),
                (() => GenerateIwflClasses, CodeGenerationOptions.IwflClasses),
            };

            if (options.All(o => !o.IsOn())) {
                yield return CodeGenerationOptions.None;

                yield break;
            }

            foreach (var option in options.Where(o => o.IsOn())) {
                yield return option.EnumValue;
            }

        }

        /// <summary>Aktiviert den Syntax-Cache der <c>nav.exe</c> (Schalter <c>/c</c>) — beschleunigt wiederholte Builds.</summary>
        public bool UseSyntaxCache { get; set; }

        /// <summary>Gibt Diagnostics mit vollständigen statt relativen Pfaden aus (Schalter <c>/fullpaths</c>).</summary>
        public bool FullPaths { get; set; }

        /// <summary>Erzeugt den generierten C#-Code im Nullable-Kontext (Schalter <c>/n</c>).</summary>
        public bool NullableContext { get; set; }

        /// <summary>Das Projekt-Wurzelverzeichnis, gegen das relative Ausgabepfade aufgelöst werden (Schalter <c>/r:</c>).</summary>
        public ITaskItem ProjectRootDirectory { get; set; }

        /// <summary>Das Wurzelverzeichnis für die generierten IWFL-Klassen (Schalter <c>/i:</c>).</summary>
        public ITaskItem IwflRootDirectory { get; set; }

        /// <summary>Das Wurzelverzeichnis für die WFL-Klassen (Schalter <c>/w:</c>).</summary>
        public ITaskItem WflRootDirectory { get; set; }

        /// <summary>
        /// Zieldatei für das Outputs-Manifest (Schalter <c>/m:</c>): die Liste der von der
        /// <c>nav.exe</c> erzeugten Dateien, Grundlage der inkrementellen Builds. Optional.
        /// </summary>
        public string ManifestFile { get; set; }

        /// <summary>
        /// Zieldatei für das Abhängigkeits-Manifest (Schalter <c>/dm:</c>): die Liste der Eingaben
        /// (inkl. <c>taskref</c>-Abhängigkeiten), gegen die der inkrementelle Build prüft. Optional.
        /// </summary>
        public string DependencyManifestFile { get; set; }

        /// <summary>
        /// Liefert den vollständigen Pfad zur <c>nav.exe</c>: sie liegt im selben Verzeichnis wie
        /// diese Task-Assembly (beide werden zusammen ausgeliefert).
        /// </summary>
        protected override string GenerateFullPathToTool() {
            // ReSharper disable once AssignNullToNotNullAttribute
            return Path.Combine(Path.GetDirectoryName(GetType().Assembly.Location), ToolName);
        }

        /// <summary>Der Dateiname des aufgerufenen Werkzeugs: <c>nav.exe</c>.</summary>
        protected override string ToolName => "nav.exe";

        /// <summary>Die Antwortdatei (Response-File) wird als UTF-8 geschrieben.</summary>
        protected override Encoding ResponseFileEncoding => Encoding.UTF8;

        /// <summary>Die Standardausgabe der <c>nav.exe</c> wird als UTF-8 gelesen (Umlaute in Diagnostics).</summary>
        protected override Encoding StandardOutputEncoding => Encoding.UTF8;

        /// <summary>
        /// Setzt die komplette Kommandozeile für die <c>nav.exe</c> aus den Task-Parametern zusammen
        /// (Schalter <c>/f</c> <c>/t</c> <c>/c</c> <c>/fullpaths</c> <c>/n</c> <c>/v</c> <c>/g:</c>
        /// <c>/r:</c> <c>/w:</c> <c>/i:</c> <c>/m:</c> <c>/dm:</c> <c>/s:</c>). Der Verbose-Schalter
        /// <c>/v</c> wird stets gesetzt; die Ausführlichkeit steuert MSBuild anschließend über die
        /// Meldungs-Wichtigkeit (siehe <see cref="LogEventsFromTextOutput"/>).
        /// </summary>
        protected override string GenerateResponseFileCommands() {

            var clb = new CommandLineBuilder();

            clb.AppendSwitchIfPresent(Force,           "/f");
            clb.AppendSwitchIfPresent(Strict,          "/t");
            clb.AppendSwitchIfPresent(UseSyntaxCache,  "/c");
            clb.AppendSwitchIfPresent(FullPaths,       "/fullpaths");
            clb.AppendSwitchIfPresent(NullableContext, "/n");
            clb.AppendSwitch("/v");
            clb.AppendSwitchIfNotNull("/g:", GetGetCodeGenerationArg());
            clb.AppendSwitchIfNotNull("/r:", ProjectRootDirectory);
            clb.AppendSwitchIfNotNull("/w:", WflRootDirectory);
            clb.AppendSwitchIfNotNull("/i:", IwflRootDirectory);
            if (!string.IsNullOrEmpty(ManifestFile)) {
                clb.AppendSwitchIfNotNull("/m:", ManifestFile);
            }

            if (!string.IsNullOrEmpty(DependencyManifestFile)) {
                clb.AppendSwitchIfNotNull("/dm:", DependencyManifestFile);
            }

            clb.AppendSwitchIfNotNull("/s:", Sources, " /s:");

            return clb.ToString();

            string GetGetCodeGenerationArg() {
                var options = GetCodeGenerationOptions().ToList();
                if (!options.Any()) {
                    return CodeGenerationOptions.None.ToString();
                }

                return string.Join(",", options);
            }
        }

        /// <summary>
        /// Prüft die Parameter vor dem Aufruf. Der Task validiert bewusst nichts selbst und liefert
        /// stets <see langword="true"/> — die eigentliche Prüfung übernimmt die <c>nav.exe</c>.
        /// </summary>
        protected override bool ValidateParameters() {
            return true;
        }

        /// <summary>
        /// Überspringt den Aufruf, wenn keine <see cref="Sources"/> anliegen — ohne
        /// Eingabedateien gibt es nichts zu generieren.
        /// </summary>
        protected override bool SkipTaskExecution() {
            return (Sources?.Length ?? 0) == 0;
        }

        /// <summary>
        /// Übersetzt eine Ausgabezeile der <c>nav.exe</c> in eine MSBuild-Meldung. Zeilen mit dem
        /// Präfix <c>Verbose:</c> werden auf <see cref="MessageImportance.Low"/> herabgestuft (und das
        /// Präfix entfernt), damit sie nur bei ausführlicher Build-Ausgabe erscheinen.
        /// </summary>
        /// <param name="singleLine">Die rohe Ausgabezeile des Werkzeugs.</param>
        /// <param name="messageImportance">Die vom Werkzeug vorgeschlagene Wichtigkeit.</param>
        protected override void LogEventsFromTextOutput(string singleLine, MessageImportance messageImportance) {

            const string verbosePrefix = "Verbose:";

            if (singleLine.StartsWith(verbosePrefix)) {
                messageImportance = MessageImportance.Low;
                singleLine        = singleLine.Substring(verbosePrefix.Length);
            }

            base.LogEventsFromTextOutput(singleLine, messageImportance);
        }

        /// <summary>
        /// Stuft die Standardausgabe des Werkzeugs standardmäßig als <see cref="MessageImportance.High"/>
        /// ein, sodass Diagnostics der <c>nav.exe</c> auch bei knapper Build-Ausgabe sichtbar bleiben.
        /// </summary>
        protected override MessageImportance StandardOutputLoggingImportance => MessageImportance.High;

    }

}
