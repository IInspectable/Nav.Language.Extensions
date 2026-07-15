#region Using Directives

using System;
using System.IO;

using JetBrains.Annotations;

#endregion

namespace Pharmatechnik.Nav.Language.Extension;

/// <summary>
/// Ein unveränderlicher Schnappschuss der Nav-<see cref="NavSolution"/> samt seines Erzeugungszeitpunkts.
/// Der <see cref="NavSolutionProvider"/> hält jeweils den aktuellen Snapshot und prüft über
/// <see cref="IsCurrent"/>, ob er noch zum aktuellen Solution-Verzeichnis und Änderungsstand passt.
/// </summary>
readonly struct NavSolutionSnapshot {

    /// <summary>
    /// Erzeugt einen Snapshot der <paramref name="solution"/> mit dem Erzeugungszeitpunkt
    /// <paramref name="creationTime"/>.
    /// </summary>
    /// <param name="creationTime">Der Zeitpunkt, zu dem der Snapshot berechnet wurde.</param>
    /// <param name="solution">Die abgebildete Nav-Solution.</param>
    public NavSolutionSnapshot(DateTime creationTime, NavSolution solution) {
        CreationTime = creationTime;
        Solution     = solution;

    }

    /// <summary>Der leere Snapshot (<see cref="NavSolution.Empty"/>, <see cref="DateTime.MinValue"/>).</summary>
    public static readonly NavSolutionSnapshot Empty = new(DateTime.MinValue, NavSolution.Empty);

    /// <summary>Der Zeitpunkt, zu dem dieser Snapshot berechnet wurde.</summary>
    public DateTime CreationTime { get; }

    /// <summary>Die von diesem Snapshot abgebildete Nav-<see cref="NavSolution"/>.</summary>
    [NotNull]
    public NavSolution Solution { get; }

    /// <summary>
    /// Prüft, ob der Snapshot noch aktuell ist: dasselbe <paramref name="solutionDirectory"/> abbildet und
    /// seit seiner Erzeugung keine spätere Dateisystem-Änderung (<paramref name="lastFileSystemChange"/>)
    /// eingetreten ist.
    /// </summary>
    /// <param name="solutionDirectory">Das aktuelle Solution-Verzeichnis.</param>
    /// <param name="lastFileSystemChange">Der Zeitpunkt der letzten beobachteten Dateisystem-Änderung.</param>
    /// <returns><c>true</c>, wenn der Snapshot noch verwendet werden kann.</returns>
    public bool IsCurrent(DirectoryInfo solutionDirectory, DateTime lastFileSystemChange) {

        if (solutionDirectory == null || Solution.SolutionDirectory == null) {
            return false;
        }

        return solutionDirectory.FullName == Solution.SolutionDirectory.FullName &&
               lastFileSystemChange       <= CreationTime;
    }

}