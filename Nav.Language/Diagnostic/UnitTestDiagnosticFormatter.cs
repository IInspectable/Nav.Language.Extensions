using System;

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Ein <see cref="DiagnosticFormatter"/> für Testerwartungen: gibt Diagnosen als Kommentarzeile aus,
/// die sich in eine <c>.nav</c>-Datei einbetten und wieder auslesen lässt. Er blendet den Dateipfad
/// aus (rechnerunabhängig) und gibt dafür Endpositionen aus (<see cref="DiagnosticFormatter.DisplayEndLocations"/>),
/// jeder Zeile ein <see cref="LinePrefix"/> und die Kategorie in Klammern vorangestellt.
/// </summary>
public class UnitTestDiagnosticFormatter: DiagnosticFormatter {

    UnitTestDiagnosticFormatter():
        base(displayEndLocations: true, workingDirectory: null) {
    }

    /// <summary>
    /// Die gemeinsam genutzte Test-Formatter-Instanz. Verdeckt (<c>new</c>) den
    /// <see cref="DiagnosticFormatter.Instance"/> der Basisklasse.
    /// </summary>
    public new static readonly DiagnosticFormatter Instance = new UnitTestDiagnosticFormatter();

    /// <summary>
    /// Das jeder formatierten Diagnose vorangestellte Präfix (<c>//==&gt;&gt;</c>), das die Zeile als
    /// Test-Erwartungskommentar kennzeichnet.
    /// </summary>
    public static String LinePrefix => "//==>>";

    /// <summary>
    /// Formatiert die Diagnose wie die Basisklasse, stellt ihr aber <see cref="LinePrefix"/> und die
    /// <see cref="Diagnostic.Category"/> in Klammern voran.
    /// </summary>
    public override string Format(Diagnostic diagnostic, IFormatProvider? formatter = null) {
        return $"{LinePrefix}[{diagnostic.Category}]{base.Format(diagnostic, formatter)}";
    }

    /// <summary>
    /// Gibt stets einen leeren Dateipfad aus, damit die Testausgabe unabhängig vom Speicherort der
    /// Quelldatei ist.
    /// </summary>
    protected override string FormatFilePath(Diagnostic diagnostic, IFormatProvider? formatter) {
        return String.Empty;
    }

}