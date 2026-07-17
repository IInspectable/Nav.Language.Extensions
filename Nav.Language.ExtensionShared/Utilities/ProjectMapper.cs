#region Using Directives

using System.Linq;
using System.Collections.Immutable;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Utilities; 

/// <summary>
/// Unveränderliche Momentaufnahme der Projekte einer Visual-Studio-Solution. Ordnet einen Dateipfad
/// dem umschließenden Projekt zu, indem das Projekt gesucht wird, dessen Verzeichnis den Dateipfad
/// enthält. Wird vom <see cref="ProjectService"/> aufgebaut.
/// </summary>
class ProjectMapper {

    private readonly ImmutableArray<ProjectInfo> _projectEntries;

    /// <summary>
    /// Erzeugt die Zuordnung über die übergebenen Projekt-Steckbriefe.
    /// </summary>
    /// <param name="projectEntries">Die Projekte der Solution.</param>
    public ProjectMapper(ImmutableArray<ProjectInfo> projectEntries) {
        _projectEntries = projectEntries;

    }

    /// <summary>
    /// Ersatz-Projektname für Dateien, die keinem Projekt der Solution zugeordnet werden können
    /// (entspricht dem VS-Knoten „Miscellaneous Files").
    /// </summary>
    public static string MiscellaneousFiles = "Miscellaneous Files";

    /// <summary>Die leere Zuordnung (keine Projekte).</summary>
    public static readonly ProjectMapper Empty = new(ImmutableArray<ProjectInfo>.Empty);

    /// <summary>
    /// Ermittelt den <see cref="ProjectInfo"/> des Projekts, dessen Verzeichnis <paramref name="fileName"/>
    /// enthält. Liefert den <c>default</c>-Steckbrief, wenn keine Zuordnung möglich ist.
    /// </summary>
    /// <param name="fileName">Der zuzuordnende Dateipfad.</param>
    public ProjectInfo GetProjectInfo(string fileName) {

        var uri = UriBuilder.BuildDirectoryUriFromFile(fileName);
        if (uri == null) {
            return default;
        }

        var projectEntry = _projectEntries.FirstOrDefault(pe => pe.ProjectDirectory.IsBaseOf(uri));

        return projectEntry;

    }

}