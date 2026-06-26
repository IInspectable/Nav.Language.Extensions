using System;

namespace Pharmatechnik.Nav.Language.BuildTasks;

// Lokale Kopie des /g:-Switch-Kontrakts der nav.exe. Bewusst hier dupliziert (statt aus Nav.Cli
// referenziert), damit der net472-Build-Task NICHT die net10-self-contained-nav.exe als Assembly
// laden muss. Maßgeblich sind allein die Enum-NAMEN: der Task serialisiert sie via ToString() in
// das /g:-Argument, nav.exe parst sie via Enum.Parse zurück (siehe Nav.Cli\CommandLine.cs).
[Flags]
internal enum CodeGenerationOptions {

    None        = 0x00,
    ToClasses   = 0x01,
    WflClasses  = 0x02,
    IwflClasses = 0x04,
    All         = ToClasses | WflClasses | IwflClasses,

}
