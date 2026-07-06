#region Using Directives

using System;
using System.Collections.Immutable;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// Das Ergebnis der Codegenerierung für genau eine <see cref="ITaskDefinitionSymbol"/>: die Liste
/// der zu schreibenden Artefakte. Menge und Zuschnitt der Artefakte sind Sache der jeweiligen
/// Generation — der nachgelagerte <see cref="IFileGenerator"/> wertet nur noch <see cref="Specs"/>
/// aus und schreibt jeden Spec gemäß seiner eigenen <see cref="CodeGenerationSpec.OverwritePolicy"/>.
/// </summary>
public sealed class CodeGenerationResult {

    /// <summary>Erzeugt das Ergebnis aus der Task-Definition und ihren generierten Specs.</summary>
    /// <param name="taskDefinition">Die Task-Definition, aus der die Artefakte erzeugt wurden.</param>
    /// <param name="specs">Die zu schreibenden Artefakte (nur nicht-leere).</param>
    public CodeGenerationResult(ITaskDefinitionSymbol taskDefinition, ImmutableArray<CodeGenerationSpec> specs) {
        TaskDefinition = taskDefinition ?? throw new ArgumentNullException(nameof(taskDefinition));
        Specs          = specs;
    }

    /// <summary>Die Task-Definition, aus der die Artefakte erzeugt wurden.</summary>
    public ITaskDefinitionSymbol TaskDefinition { get; }

    /// <summary>Die zu schreibenden Artefakte dieser Task-Definition (leere Specs sind ausgefiltert).</summary>
    public ImmutableArray<CodeGenerationSpec> Specs { get; }

}