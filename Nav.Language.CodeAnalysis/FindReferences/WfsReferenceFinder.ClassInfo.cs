namespace Pharmatechnik.Nav.Language.CodeAnalysis.FindReferences; 

public static partial class WfsReferenceFinder {

    /// <summary>
    /// Beschreibt eine <see cref="NavlessClasses">nav-lose WFS-Klasse</see>: die Zuordnung von
    /// Projektname zu voll qualifiziertem Typnamen der handgeschriebenen WFS-Klasse ohne
    /// <c>.nav</c>-Quelle, nach der <see cref="FindReferencesAsync"/> im jeweiligen Projekt sucht.
    /// </summary>
    readonly struct ClassInfo {

        /// <summary>Erzeugt eine <see cref="ClassInfo"/> aus Projektname und voll qualifiziertem Typnamen.</summary>
        /// <param name="projectName">Der Name des Roslyn-Projekts, das die Klasse enthält.</param>
        /// <param name="className">Der voll qualifizierte Metadaten-Name der WFS-Klasse.</param>
        public ClassInfo(string projectName, string className) {
            ProjectName = projectName;
            ClassName   = className;
        }

        /// <summary>Der Name des Roslyn-Projekts, in dem die WFS-Klasse liegt.</summary>
        public string ProjectName { get; }
        /// <summary>Der voll qualifizierte Metadaten-Name der WFS-Klasse.</summary>
        public string ClassName   { get; }

    }

}