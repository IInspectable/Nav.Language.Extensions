namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Implementierung von <see cref="IExitConnectionPointReferenceSymbol"/>; entsteht im
/// <see cref="TaskDefinitionSymbolBuilder"/> beim Binden einer Exit-Transition — allerdings nur,
/// wenn der Exit-Name im Quelltext vorhanden und der Quellknoten als Task-Knoten auflösbar ist.
/// </summary>
sealed partial class ExitConnectionPointReferenceSymbol: Symbol, IExitConnectionPointReferenceSymbol {

    /// <summary>Initialisiert die Referenz.</summary>
    /// <param name="syntaxTree">Der Syntaxbaum, in dem die Referenz steht.</param>
    /// <param name="name">Der referenzierte Exit-Name, wie er im Quelltext steht.</param>
    /// <param name="location">Die Fundstelle der Referenz.</param>
    /// <param name="connectionPoint">Der aufgelöste Verbindungspunkt, oder <c>null</c> bei unauflösbarem Namen.</param>
    public ExitConnectionPointReferenceSymbol(SyntaxTree syntaxTree, string name, Location location, IExitConnectionPointSymbol? connectionPoint)
        : base(name, location) {
        SyntaxTree  = syntaxTree;
        Declaration = connectionPoint;
    }

    /// <inheritdoc/>
    public override SyntaxTree SyntaxTree { get; }

    /// <inheritdoc/>
    public IExitConnectionPointSymbol? Declaration { get; }

    // Wird definitiv im Ctor der ExitTransition gesetzt — in der "freien Wildbahn" darf der
    // Null-Fall nicht auftreten.
    /// <inheritdoc/>
    /// <remarks>
    /// Wird während der Modell-Konstruktion vom Konstruktor der Exit-Transition gesetzt (interner
    /// Setter); am fertigen Semantikmodell nie <c>null</c>.
    /// </remarks>
    public IExitTransition ExitTransition { get; internal set; } = null!;

}
