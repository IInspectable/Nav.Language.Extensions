using System;

namespace Pharmatechnik.Nav.Language.BuildTasks;

// Lokale Kopie des /g:-Switch-Kontrakts der nav.exe. Bewusst hier dupliziert (statt aus Nav.Cli
// referenziert), damit der net472-Build-Task NICHT die net10-self-contained-nav.exe als Assembly
// laden muss. Maßgeblich sind allein die Enum-NAMEN: der Task serialisiert sie via ToString() in
// das /g:-Argument, nav.exe parst sie via Enum.Parse zurück (siehe Nav.Cli\CommandLine.cs).
/// <summary>
/// Die per <c>/g:</c>-Schalter der <c>nav.exe</c> anforderbaren Codegen-Artefakt-Klassen — als
/// <see cref="FlagsAttribute">Flags</see>-Enum, damit sich mehrere Klassenarten zu einem Schalterwert
/// kombinieren lassen.
/// </summary>
/// <remarks>
/// Bewusst als <b>lokale Kopie</b> des <c>/g:</c>-Kontrakts geführt (statt aus dem CLI-Host
/// referenziert), damit der net472-Build-Task nicht die net10-self-contained <c>nav.exe</c> als
/// Assembly laden muss. Maßgeblich für die Interoperabilität sind allein die Enum-<b>Namen</b>: der
/// Task serialisiert die gewählten Werte über <see cref="object.ToString"/> in das
/// <c>/g:</c>-Argument, die <c>nav.exe</c> parst sie per <c>Enum.Parse</c> wieder ein. Die
/// Zahlenwerte müssen daher nicht mit denen der CLI übereinstimmen, die Namen schon.
/// </remarks>
[Flags]
internal enum CodeGenerationOptions {

    /// <summary>Keine Artefakt-Klasse angefordert — es wird nur validiert, aber nichts erzeugt.</summary>
    None = 0x00,

    /// <summary>Die TO-Klassen (Transfer-Objekte). Bewusst opt-in und nicht Teil von <see cref="All"/>.</summary>
    ToClasses = 0x01,

    /// <summary>Die WFL-Klassen (Workflow-Logik, die vom Entwickler zu implementierende Seite).</summary>
    WflClasses = 0x02,

    /// <summary>Die IWFL-Klassen (die generierte Infrastruktur-Seite des Workflows).</summary>
    IwflClasses = 0x04,

    // TO-Klassen sind bewusst opt-in und NICHT Teil von All (siehe GenerationOptions.GenerateToClasses).
    /// <summary>
    /// Der Standardumfang: <see cref="WflClasses"/> und <see cref="IwflClasses"/>. Die
    /// <see cref="ToClasses"/> sind hier absichtlich nicht enthalten (opt-in).
    /// </summary>
    All = WflClasses | IwflClasses,

}
