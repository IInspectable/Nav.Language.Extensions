#region Using Directives

using System;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Utilities;

/// <summary>
/// Unveränderlicher Steckbrief eines Projekts der Visual-Studio-Solution: Verzeichnis-<see cref="Uri"/>,
/// Anzeigename und Projekt-GUID. Wird vom <see cref="ProjectService"/> aus jeder <see cref="Hierarchy"/>
/// erzeugt und im <see cref="ProjectMapper"/> gehalten, um eine Datei ihrem Projekt zuzuordnen.
/// </summary>
readonly struct ProjectInfo {

    private readonly string _name;

    /// <summary>
    /// Erzeugt einen Projekt-Steckbrief.
    /// </summary>
    /// <param name="directory">Verzeichnis-<see cref="Uri"/> des Projekts (mit abschließendem Trenner).</param>
    /// <param name="name">Anzeigename des Projekts; darf nicht <c>null</c> sein.</param>
    /// <param name="projectGuid">Eindeutige Projekt-GUID.</param>
    public ProjectInfo(Uri directory, string name, Guid projectGuid) {

        _name            = name      ?? throw new ArgumentNullException(nameof(name));
        ProjectDirectory = directory ?? throw new ArgumentNullException(nameof(directory));
        ProjectGuid      = projectGuid;
    }

    /// <summary>
    /// Anzeigename des Projekts; ersatzweise <see cref="ProjectMapper.MiscellaneousFiles"/>, falls kein
    /// Name gesetzt ist (z.B. beim <c>default</c>-Steckbrief einer nicht zugeordneten Datei).
    /// </summary>
    public string ProjectName      => _name ?? ProjectMapper.MiscellaneousFiles;
    /// <summary>Verzeichnis-<see cref="Uri"/> des Projekts (mit abschließendem Trenner).</summary>
    public Uri    ProjectDirectory { get; }
    /// <summary>Eindeutige Projekt-GUID.</summary>
    public Guid   ProjectGuid      { get; }

}