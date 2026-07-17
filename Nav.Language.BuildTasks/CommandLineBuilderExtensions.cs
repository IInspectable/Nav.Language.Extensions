#region Using Directives

using Microsoft.Build.Utilities;

#endregion

namespace Pharmatechnik.Nav.Language.BuildTasks;

/// <summary>
/// Erweiterungsmethoden für den <see cref="CommandLineBuilder"/>, mit denen der
/// <see cref="Nav"/>-Task seine Antwortdatei (Response-File) für die <c>nav.exe</c> zusammensetzt.
/// </summary>
static class CommandLineBuilderExtensions {

    /// <summary>
    /// Hängt einen boolschen Schalter nur dann an, wenn er gesetzt ist. Anders als
    /// <see cref="CommandLineBuilder.AppendSwitch(string)"/> wird bei <paramref name="switchValue"/>
    /// == <see langword="false"/> gar nichts geschrieben — so entstehen keine leeren/negierten Schalter.
    /// </summary>
    /// <param name="commandLineBuilder">Der Builder, an den der Schalter angehängt wird.</param>
    /// <param name="switchValue">Steuert, ob der Schalter überhaupt angehängt wird.</param>
    /// <param name="switchName">Der anzuhängende Schaltername (z.B. <c>/f</c>).</param>
    public static void AppendSwitchIfPresent(this CommandLineBuilder commandLineBuilder, bool switchValue, string switchName) {
        if (switchValue) {
            commandLineBuilder.AppendSwitch(switchName);
        }
    }

}
