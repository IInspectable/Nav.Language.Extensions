#region Using Directives

using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;

#endregion

namespace Pharmatechnik.Nav.Language.Dependencies; 

/// <summary>
/// Ermittelt die Aufruf-Abhängigkeiten zwischen Task-Definitionen: Für jeden über eine eingehende
/// Kante tatsächlich genutzten <c>task</c>-Knoten (<see cref="ITaskNodeSymbol"/>) mit aufgelöster
/// Deklaration entsteht eine <see cref="Dependency"/> vom Knoten (nutzende Seite) auf die referenzierte
/// Task-Deklaration (genutzte Seite). Die drei Überladungen fächern von der Übersetzungseinheit
/// (<see cref="CodeGenerationUnit"/>) bis zur einzelnen <see cref="ITaskDefinitionSymbol"/> auf.
/// </summary>
public static class DependencyAnalyzer {

    /// <summary>
    /// Sammelt die Task-Abhängigkeiten über mehrere <see cref="CodeGenerationUnit"/> hinweg.
    /// </summary>
    public static ImmutableList<Dependency> CollectTasksDefinitionDependencies(IEnumerable<CodeGenerationUnit> codeGenerationUnits) {
        return codeGenerationUnits.SelectMany(CollectTasksDefinitionDependencies).ToImmutableList();
    }

    /// <summary>
    /// Sammelt die Task-Abhängigkeiten aller Task-Definitionen einer <see cref="CodeGenerationUnit"/>.
    /// </summary>
    public static ImmutableList<Dependency> CollectTasksDefinitionDependencies(CodeGenerationUnit codeGenerationUnit) {
        return codeGenerationUnit.TaskDefinitions.SelectMany(CollectTasksDefinitionDependencies).ToImmutableList();
    }

    /// <summary>
    /// Sammelt die Task-Abhängigkeiten einer einzelnen Task-Definition: berücksichtigt werden nur
    /// <see cref="ITaskNodeSymbol"/>-Knoten mit mindestens einer eingehenden Kante (also tatsächlich
    /// erreichte Aufrufe) und aufgelöster <see cref="ITaskNodeSymbol.Declaration"/>.
    /// </summary>
    public static ImmutableList<Dependency> CollectTasksDefinitionDependencies(ITaskDefinitionSymbol taskDefinition) {
        return taskDefinition.NodeDeclarations
                             .OfType<ITaskNodeSymbol>()
                             .Where(tn => tn.Incomings.Any() && tn.Declaration != null)
                             .Select(taskNode => new Dependency(
                                         usingItem: DependencyItem.FromSymbol(taskNode),
                                         // Declaration ist durch den Where-Filter oben non-null; das Narrowing
                                         // trägt nicht in die Select-Lambda, daher die begründete Suppression.
                                         usedItem : DependencyItem.FromSymbol(taskNode.Declaration!)))
                             .ToImmutableList();         
    }
}

// VersionStamp
// BackgroundParser