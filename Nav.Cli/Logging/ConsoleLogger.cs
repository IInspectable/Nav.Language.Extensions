#region Using Directives

using System;

using Pharmatechnik.Nav.Language.Generator;

#endregion

namespace Pharmatechnik.Nav.Language.Logging; 

/// <summary>
/// Die Konsolen-Implementierung der Engine-Abstraktion <see cref="ILogger"/> für den CLI-Host: schreibt
/// Meldungen farbig nach der Standardausgabe, je Schweregrad in einer eigenen Farbe. <see cref="Diagnostic"/>s
/// werden über einen <see cref="DiagnosticFormatter"/> aufbereitet. Die Flags <see cref="Verbose"/>,
/// <see cref="NoWarnings"/> und <see cref="FullPaths"/> steuern Umfang und Pfaddarstellung; sie werden vom
/// <see cref="CommandLine"/>-Modell gespeist. Die eigentliche Ausgabe erfolgt über <c>protected virtual</c>
/// <c>Write*</c>-Haken, die abgeleitete Logger überschreiben können.
/// </summary>
public class ConsoleLogger : ILogger {

    /// <summary>
    /// Erzeugt einen Konsolen-Logger.
    /// </summary>
    /// <param name="fullPaths">Ob in der Ausgabe vollständige Pfade verwendet werden (siehe <see cref="FullPaths"/>).</param>
    /// <param name="noWarnings">Ob Warnungen unterdrückt werden (siehe <see cref="NoWarnings"/>).</param>
    /// <param name="verbose">Ob ausführliche Meldungen ausgegeben werden (siehe <see cref="Verbose"/>).</param>
    /// <param name="verbosePrefix">Das Präfix vor ausführlichen Meldungen; <see langword="null"/> wählt den
    /// Standard <c>"Verbose:"</c>.</param>
    public ConsoleLogger(bool fullPaths, bool noWarnings = false, bool verbose = false, string verbosePrefix = null) {
        FullPaths     = fullPaths;
        NoWarnings    = noWarnings;
        Verbose       = verbose;
        VerbosePrefix = verbosePrefix ?? "Verbose:";
    }

    /// <summary>Ob in der Ausgabe vollständige Pfade statt gegen das Arbeitsverzeichnis verkürzter Pfade
    /// verwendet werden (steuert die Pfaddarstellung in <see cref="FormatDiagnostic"/>).</summary>
    public bool   FullPaths     { get; }
    /// <summary>Ob ausführliche Meldungen ausgegeben werden; ist der Schalter aus, verwirft
    /// <see cref="LogVerbose"/> die Meldung.</summary>
    public bool   Verbose       { get; }
    /// <summary>Ob Warnungen unterdrückt werden; ist der Schalter an, verwirft <see cref="LogWarning"/>
    /// die Meldung.</summary>
    public bool   NoWarnings    { get;}
    /// <summary>Das Präfix, das <see cref="WriteVerbose"/> jeder ausführlichen Meldung voranstellt.</summary>
    public string VerbosePrefix { get; }

    /// <summary>Gibt eine ausführliche Meldung aus — nur, wenn <see cref="Verbose"/> gesetzt ist.</summary>
    /// <param name="message">Der Meldungstext.</param>
    public void LogVerbose(string message) {
        if (!Verbose) {
            return;
        }
        WriteVerbose(message);
    }
        
    /// <summary>Gibt eine Informationsmeldung aus.</summary>
    /// <param name="message">Der Meldungstext.</param>
    public void LogInfo(string message) {
        WriteInfo(message);
    }

    /// <summary>Gibt eine Fehlermeldung aus.</summary>
    /// <param name="message">Der Meldungstext.</param>
    public void LogError(string message) {
        WriteError(message);
    }

    /// <summary>Gibt eine <see cref="Diagnostic"/> als Fehlermeldung aus (über <see cref="FormatDiagnostic"/>
    /// aufbereitet).</summary>
    /// <param name="diag">Die auszugebende Diagnose.</param>
    public void LogError(Diagnostic diag) {
        WriteError(FormatDiagnostic(diag));
    }

    /// <summary>Gibt eine <see cref="Diagnostic"/> als Warnung aus — nur, wenn <see cref="NoWarnings"/>
    /// nicht gesetzt ist.</summary>
    /// <param name="diag">Die auszugebende Diagnose.</param>
    public void LogWarning(Diagnostic diag) {
        if (NoWarnings) {
            return;
        }
        WriteWarning(FormatDiagnostic(diag));
    }

    /// <summary>Ausgabe-Haken für ausführliche Meldungen (grau, mit <see cref="VerbosePrefix"/>).
    /// Überschreibbar, um die Ausgabe umzuleiten.</summary>
    /// <param name="message">Der Meldungstext.</param>
    protected virtual void WriteVerbose(string message) {
        WriteLine($"{VerbosePrefix}{message}", ConsoleColor.DarkGray);
    }

    /// <summary>Ausgabe-Haken für Informationsmeldungen (Standardfarbe). Überschreibbar, um die Ausgabe
    /// umzuleiten.</summary>
    /// <param name="message">Der Meldungstext.</param>
    protected virtual void WriteInfo(string message) {
        WriteLine($"{message}", Console.ForegroundColor);
    }

    /// <summary>Ausgabe-Haken für Fehlermeldungen (rot). Überschreibbar, um die Ausgabe umzuleiten.</summary>
    /// <param name="message">Der Meldungstext.</param>
    protected virtual void WriteError(string message) {
        WriteLine(message, ConsoleColor.Red);
    }

    /// <summary>Ausgabe-Haken für Warnungen (gelb). Überschreibbar, um die Ausgabe umzuleiten.</summary>
    /// <param name="message">Der Meldungstext.</param>
    protected virtual void WriteWarning(string message) {
        WriteLine(message, ConsoleColor.Yellow);
    }

    /// <summary>Schreibt eine Zeile in der angegebenen Vordergrundfarbe und stellt die vorherige Farbe
    /// anschließend wieder her.</summary>
    /// <param name="message">Der auszugebende Text.</param>
    /// <param name="foregroundColor">Die zu verwendende Vordergrundfarbe.</param>
    void WriteLine(string message, ConsoleColor foregroundColor) {
        var oldBackground = Console.ForegroundColor;
        try {
            Console.ForegroundColor = foregroundColor;
            Console.WriteLine(message);
        } finally {
            Console.ForegroundColor = oldBackground;
        }
    }

    /// <summary>Bereitet eine <see cref="Diagnostic"/> über einen <see cref="DiagnosticFormatter"/> als
    /// Text auf. <see cref="FullPaths"/> steuert dabei, ob End-Positionen angezeigt und ob Pfade absolut
    /// (statt gegen das aktuelle Arbeitsverzeichnis verkürzt) dargestellt werden.</summary>
    /// <param name="diag">Die zu formatierende Diagnose.</param>
    /// <returns>Der formatierte Meldungstext.</returns>
    protected virtual string FormatDiagnostic(Diagnostic diag) {
        var formatter = new DiagnosticFormatter(
            displayEndLocations: FullPaths,
            workingDirectory   : FullPaths ? null : Environment.CurrentDirectory);

        return formatter.Format(diag);
    } 
}