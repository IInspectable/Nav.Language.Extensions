// ReSharper disable InconsistentNaming

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Bestimmt die Dateipfade der aus einer Task erzeugten C#-Artefakte (Ablageorte und Namen der
/// generierten sowie der benutzergepflegten Dateien). Kapselt die Zielverzeichnis-/Namenslogik des
/// Codegenerators, sodass Aufrufer nicht mit den Namenskonventionen selbst umgehen müssen.
/// </summary>
public interface IPathProvider {

    /// <summary>Der Pfad der zugrunde liegenden <c>.nav</c>-Quelldatei.</summary>
    string SyntaxFileName    { get; }
    /// <summary>Der Pfad der generierten Basisklassen-Datei der Task (<c>{Task}WFSBase.generated.cs</c>).</summary>
    string WfsBaseFileName   { get; }
    /// <summary>Der Pfad der generierten Schnittstellen-Datei der Task (<c>I{Task}WFS.generated.cs</c>).</summary>
    string IWfsFileName      { get; }
    /// <summary>Der Pfad der generierten Begin-Schnittstellen-Datei der Task (<c>IBegin{Task}WFS.generated.cs</c>).</summary>
    string IBeginWfsFileName { get; }
    /// <summary>Der Pfad der benutzergepflegten (nicht generierten) Task-Klassen-Datei (<c>{Task}WFS.cs</c>).</summary>
    string WfsFileName       { get; }

    /// <summary>
    /// Liefert den Pfad der generierten Datei für eine benannte „To"-Klasse.
    /// </summary>
    /// <param name="toClassName">Der Name der To-Klasse.</param>
    string GetToFileName(string toClassName);
    /// <summary>
    /// Liefert den relativen Pfad von <paramref name="fromPath"/> nach <paramref name="toPath"/>.
    /// </summary>
    /// <param name="fromPath">Der Ausgangspfad.</param>
    /// <param name="toPath">Der Zielpfad.</param>
    string GetRelativePath(string fromPath, string toPath);

}