using Pharmatechnik.Nav.Language.Internal;

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Implementierung von <see cref="INodeReferenceSymbol"/> — zugleich der untypisierte Rückfall des
/// <see cref="TaskDefinitionSymbolBuilder"/> für Referenzen, deren Name sich nicht auflösen lässt
/// (<see cref="Declaration"/> ist dann <c>null</c>); die aufgelösten Fälle erzeugen die typisierten
/// Ableitungen von <see cref="NodeReferenceSymbol{T}"/>. Bewusst unversiegelt, da
/// <see cref="NodeReferenceSymbol{T}"/> ableitet.
/// </summary>
[SuppressCodeSanityCheck("NodeReferenceSymbol darf hier unversiegelt bestehen.")]
partial class NodeReferenceSymbol: Symbol, INodeReferenceSymbol {

    /// <summary>Initialisiert die Referenz.</summary>
    /// <param name="syntaxTree">Der Syntaxbaum, in dem die Referenz steht.</param>
    /// <param name="name">Der referenzierte Knotenname, wie er im Quelltext steht.</param>
    /// <param name="location">Die Fundstelle der Referenz.</param>
    /// <param name="declaration">Der aufgelöste Knoten, oder <c>null</c> bei unauflösbarem Namen.</param>
    /// <param name="nodeReferenceType">Ob die Referenz als Quelle oder Ziel ihrer Kante steht.</param>
    public NodeReferenceSymbol(SyntaxTree syntaxTree, string name, Location location, INodeSymbol? declaration, NodeReferenceType nodeReferenceType)
        : base(name, location) {
        SyntaxTree        = syntaxTree;
        NodeReferenceType = nodeReferenceType;
        Declaration       = declaration;
    }

    /// <inheritdoc/>
    public override SyntaxTree SyntaxTree { get; }

    /// <inheritdoc/>
    public INodeSymbol? Declaration { get; }

    /// <inheritdoc/>
    public NodeReferenceType NodeReferenceType { get; }

    // Wird im Ctor der Edge während der Initialisierung gesetzt — in der "freien Wildbahn" darf
    // der Null-Fall nicht auftreten.
    /// <inheritdoc/>
    /// <remarks>
    /// Wird während der Modell-Konstruktion vom Konstruktor der Kante gesetzt (interner Setter);
    /// am fertigen Semantikmodell nie <c>null</c>.
    /// </remarks>
    public IEdge Edge { get; internal set; } = null!;

}

/// <summary>
/// Implementierung von <see cref="ICancelNodeReferenceSymbol"/> — die Zielreferenz eines
/// <c>cancel</c>-Kantenziels. Sie löst sich per Design nie auf (<see cref="NodeReferenceSymbol.Declaration"/>
/// stets <c>null</c>): <c>cancel</c> hat keine Deklaration (E4). Eine eigene, versiegelte Ableitung — statt
/// des untypisierten Rückfalls <see cref="NodeReferenceSymbol"/> — macht das <c>cancel</c>-Ziel am fertigen
/// Modell als eigener Fall erkennbar (<see cref="EdgeExtensions.TargetsCancel"/>) und grenzt es vom echten
/// „unauflösbaren Name" (Nav0011) ab.
/// </summary>
sealed partial class CancelNodeReferenceSymbol: NodeReferenceSymbol, ICancelNodeReferenceSymbol {

    /// <summary>Initialisiert die stets unaufgelöste <c>cancel</c>-Zielreferenz.</summary>
    public CancelNodeReferenceSymbol(SyntaxTree syntaxTree, string name, Location location, NodeReferenceType nodeReferenceType)
        : base(syntaxTree, name, location, declaration: null, nodeReferenceType) {
    }

}
