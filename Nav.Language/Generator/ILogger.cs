namespace Pharmatechnik.Nav.Language.Generator;

/// <summary>
/// Senke für die Fortschritts- und Diagnose-Ausgabe eines Codegenerator-Laufs
/// (<see cref="NavCodeGeneratorPipeline.Run"/>). Der Host stellt die Implementierung bereit und
/// entscheidet über Ziel und Formatierung — <c>Nav.Cli</c> etwa mit einem farbigen
/// Konsolen-Logger. Die Pipeline ruft die Methoden nicht direkt, sondern über ihren internen
/// <c>LoggerAdapter</c> (Zeitmessung, Dubletten-Unterdrückung).
/// </summary>
public interface ILogger {
    /// <summary>Protokolliert eine ausführliche Meldung (nur relevant bei aktivem Verbose-Modus des
    /// Hosts), etwa den Beginn/das Ende der Verarbeitung einer Datei.</summary>
    /// <param name="message">Der Meldungstext.</param>
    void LogVerbose(string message);
    /// <summary>Protokolliert eine Informationsmeldung, etwa die Abschluss-Statistik eines
    /// Laufs.</summary>
    /// <param name="message">Der Meldungstext.</param>
    void LogInfo(string message);
    /// <summary>Protokolliert eine Warnung aus einem <see cref="Diagnostic"/> (Warn-Schweregrad).</summary>
    /// <param name="diag">Die zu meldende Diagnose.</param>
    void LogWarning(Diagnostic diag);
    /// <summary>Protokolliert eine Fehlermeldung als freien Text (etwa eine nicht als
    /// <see cref="Diagnostic"/> vorliegende Ausnahme).</summary>
    /// <param name="message">Der Fehlertext.</param>
    void LogError(string message);
    /// <summary>Protokolliert einen Fehler aus einem <see cref="Diagnostic"/> (Fehler-Schweregrad).</summary>
    /// <param name="diag">Die zu meldende Diagnose.</param>
    void LogError(Diagnostic diag);
}