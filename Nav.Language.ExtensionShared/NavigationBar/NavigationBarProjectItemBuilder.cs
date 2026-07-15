#region Using Directives

using System.Collections.Immutable;

using Microsoft.VisualStudio.Shell;

using Pharmatechnik.Nav.Language.Extension.Common;
using Pharmatechnik.Nav.Language.Extension.Images;
using Pharmatechnik.Nav.Language.Extension.Utilities;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.NavigationBar;

/// <summary>
/// Baut den (einzelnen) Eintrag der Projekt-Combobox der Navigationsleiste: den Namen des enthaltenden
/// Projekts der Datei (bzw. <see cref="ProjectMapper.MiscellaneousFiles"/>, wenn keinem Projekt
/// zugeordnet). Da die Projektzuordnung den UI-Thread erfordert, läuft der Bau dort.
/// </summary>
class NavigationBarProjectItemBuilder {

    /// <summary>Liefert den Projekteintrag zum aktuellen Modell (leere Liste ohne Modell). Nur auf dem UI-Thread.</summary>
    public static ImmutableList<NavigationBarItem> Build(CodeGenerationUnitAndSnapshot codeGenerationUnitAndSnapshot) {

        // GetContainingProject muss auf dem Main Thread aufgerufen werden.
        ThreadHelper.ThrowIfNotOnUIThread();

        if (codeGenerationUnitAndSnapshot == null) {
            return ImmutableList<NavigationBarItem>.Empty;
        }

        return new[] {
            new NavigationBarItem(
                displayName: codeGenerationUnitAndSnapshot.Snapshot.TextBuffer.GetContainingProject()?.Name ?? ProjectMapper.MiscellaneousFiles,
                imageMoniker: ImageMonikers.ProjectNode)
        }.ToImmutableList();
    }

}