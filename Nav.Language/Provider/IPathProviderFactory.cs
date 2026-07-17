using Pharmatechnik.Nav.Language.CodeGen;

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Erzeugt einen <see cref="IPathProvider"/> für eine konkrete Task-Definition unter den gegebenen
/// Generierungsoptionen (Wurzelverzeichnisse, Sprach-Version).
/// </summary>
public interface IPathProviderFactory {

    /// <summary>
    /// Erzeugt den Pfad-Provider für <paramref name="taskDefinition"/>.
    /// </summary>
    /// <param name="taskDefinition">Die Task, deren Artefakt-Pfade bestimmt werden.</param>
    /// <param name="options">Die Generierungsoptionen (u.a. Wurzelverzeichnisse).</param>
    IPathProvider CreatePathProvider(ITaskDefinitionSymbol taskDefinition, GenerationOptions options);

}