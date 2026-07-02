#nullable enable

namespace Pharmatechnik.Nav.Language;

public static class TaskDefinitionSymbolExtensions {

    public static INodeSymbol? TryFindNode(this ITaskDefinitionSymbol? taskDefinition, string? name) {
        return taskDefinition?.NodeDeclarations.TryFindSymbol(name);
    }

    public static T? TryFindNode<T>(this ITaskDefinitionSymbol? taskDefinition, string? name) where T : class, INodeSymbol {
        return taskDefinition?.NodeDeclarations.TryFindSymbol(name) as T;
    }

}
