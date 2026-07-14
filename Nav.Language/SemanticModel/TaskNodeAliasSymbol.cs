namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Implementierung von <see cref="ITaskNodeAliasSymbol"/>. Entsteht im
/// <see cref="TaskDefinitionSymbolBuilder"/> vor dem zugehörigen Task-Knoten; die Rückreferenz
/// <see cref="TaskNode"/> setzt anschließend der Konstruktor des <see cref="TaskNodeSymbol"/>.
/// </summary>
sealed partial class TaskNodeAliasSymbol: Symbol, ITaskNodeAliasSymbol {

    public TaskNodeAliasSymbol(SyntaxTree syntaxTree, string name, Location location): base(name, location) {
        SyntaxTree = syntaxTree;
    }

    /// <inheritdoc/>
    public override SyntaxTree SyntaxTree { get; }

    // Wird im Ctor der TaskNode festgelegt — in der "freien Wildbahn" darf der Null-Fall nicht auftreten.
    /// <inheritdoc/>
    /// <remarks>
    /// Wird bei der Konstruktion des Task-Knotens gesetzt (interner Setter); am fertigen
    /// Semantikmodell nie <c>null</c>.
    /// </remarks>
    public ITaskNodeSymbol TaskNode { get; internal set; } = null!;

}
