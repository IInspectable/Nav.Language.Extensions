namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Implementierung von <see cref="IInitNodeAliasSymbol"/>. Entsteht im
/// <see cref="TaskDefinitionSymbolBuilder"/> vor dem zugehörigen Init-Knoten; die Rückreferenz
/// <see cref="InitNode"/> setzt anschließend der Konstruktor des <see cref="InitNodeSymbol"/>.
/// </summary>
sealed partial class InitNodeAliasSymbol: Symbol, IInitNodeAliasSymbol {

    public InitNodeAliasSymbol(SyntaxTree syntaxTree, string name, Location location): base(name, location) {
        SyntaxTree = syntaxTree;
    }

    /// <inheritdoc/>
    public override SyntaxTree SyntaxTree { get; }

    // Wird im Ctor der InitNode festgelegt — in der "freien Wildbahn" darf der Null-Fall nicht auftreten.
    /// <inheritdoc/>
    /// <remarks>
    /// Wird bei der Konstruktion des Init-Knotens gesetzt (interner Setter); am fertigen
    /// Semantikmodell nie <c>null</c>.
    /// </remarks>
    public IInitNodeSymbol InitNode { get; internal set; } = null!;

}
