#region Using Directives

using System;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// Das Ergebnis des Schreibvorgangs für genau eine Datei: welche Task-Definition sie erzeugt hat,
/// welcher Pfad geschrieben wurde und ob die Datei aktualisiert oder übersprungen wurde. Der
/// <see cref="FileGenerator"/> liefert je verarbeitetem <see cref="CodeGenerationSpec"/> einen
/// solchen Datensatz.
/// </summary>
public record FileGeneratorResult {

    /// <summary>Erzeugt das Schreibergebnis aus Task-Definition, ausgeführter <paramref name="action"/> und Zielpfad.</summary>
    // ReSharper disable once ConvertToPrimaryConstructor
    public FileGeneratorResult(ITaskDefinitionSymbol taskDefinition, FileGeneratorAction action, string fileName) {
        TaskDefinition = taskDefinition ?? throw new ArgumentNullException(nameof(taskDefinition));
        FileName       = fileName       ?? throw new ArgumentNullException(nameof(fileName));
        Action         = action;
    }

    /// <summary>Die Task-Definition, aus der die geschriebene Datei erzeugt wurde.</summary>
    public ITaskDefinitionSymbol TaskDefinition { get; }
    /// <summary>Der Pfad der geschriebenen bzw. übersprungenen Datei.</summary>
    public string                FileName       { get; }
    /// <summary>Ob die Datei geschrieben (<see cref="FileGeneratorAction.Updated"/>) oder übersprungen (<see cref="FileGeneratorAction.Skiped"/>) wurde.</summary>
    public FileGeneratorAction   Action         { get; }

}