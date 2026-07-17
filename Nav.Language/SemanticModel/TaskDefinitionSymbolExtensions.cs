namespace Pharmatechnik.Nav.Language;

/// <summary>Null-tolerante Knoten-Lookups auf <see cref="ITaskDefinitionSymbol"/>.</summary>
public static class TaskDefinitionSymbolExtensions {

    /// <summary>
    /// Liefert den deklarierten Knoten mit dem angegebenen Namen aus
    /// <see cref="ITaskDefinitionSymbol.NodeDeclarations"/>, oder <c>null</c> — auch wenn
    /// Definition oder Name <c>null</c> sind.
    /// </summary>
    /// <param name="taskDefinition">Die zu durchsuchende Task-Definition, oder <c>null</c>.</param>
    /// <param name="name">Der gesuchte Knotenname, oder <c>null</c>.</param>
    public static INodeSymbol? TryFindNode(this ITaskDefinitionSymbol? taskDefinition, string? name) {
        return taskDefinition?.NodeDeclarations.TryFindSymbol(name);
    }

    /// <summary>
    /// Typisierte Variante von <see cref="TryFindNode(ITaskDefinitionSymbol, string)"/>: liefert
    /// den Knoten nur, wenn er vom Typ <typeparamref name="T"/> ist — sonst <c>null</c>.
    /// </summary>
    /// <typeparam name="T">Die erwartete Knoten-Art (z.B. <see cref="ITaskNodeSymbol"/>).</typeparam>
    /// <param name="taskDefinition">Die zu durchsuchende Task-Definition, oder <c>null</c>.</param>
    /// <param name="name">Der gesuchte Knotenname, oder <c>null</c>.</param>
    public static T? TryFindNode<T>(this ITaskDefinitionSymbol? taskDefinition, string? name) where T : class, INodeSymbol {
        return taskDefinition?.NodeDeclarations.TryFindSymbol(name) as T;
    }

}
