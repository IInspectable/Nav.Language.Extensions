namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Die Seite einer Kante, auf der eine Knoten-Referenz (<see cref="INodeReferenceSymbol"/>)
/// steht: als Quelle oder als Ziel der Transition.
/// </summary>
public enum NodeReferenceType {

    /// <summary>Die Referenz steht auf der Quellseite der Kante.</summary>
    Source,
    /// <summary>Die Referenz steht auf der Zielseite der Kante.</summary>
    Target

}

/// <summary>
/// Symbol einer Verwendung eines Knotens im Transitionsblock — z.B. stehen in
/// <c>Start --&gt; Auswahl;</c> zwei Knoten-Referenzen, links als Quelle, rechts als Ziel. Anders
/// als die Deklaration (<see cref="INodeSymbol"/>) steht eine Referenz für genau eine Fundstelle
/// an genau einer Kante (<see cref="Edge"/>); alle Referenzen eines Knotens sammelt
/// <see cref="INodeSymbol.References"/>.
/// </summary>
public interface INodeReferenceSymbol: ISymbol {

    /// <summary>
    /// Der deklarierte Knoten, auf den sich diese Referenz auflöst — <c>null</c>, wenn im
    /// umgebenden Task kein Knoten dieses Namens deklariert ist (Diagnose Nav0011).
    /// </summary>
    INodeSymbol? Declaration { get; }

    /// <summary>Ob diese Referenz auf der Quell- oder der Zielseite ihrer Kante steht.</summary>
    NodeReferenceType NodeReferenceType { get; }

    /// <summary>Die Kante (Transition), zu der diese Referenz gehört.</summary>
    IEdge Edge { get; }

}
