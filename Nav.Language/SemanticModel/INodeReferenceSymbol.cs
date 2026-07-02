#nullable enable

namespace Pharmatechnik.Nav.Language;

public enum NodeReferenceType {

    Source,
    Target

}

public interface INodeReferenceSymbol: ISymbol {

    INodeSymbol? Declaration { get; }

    NodeReferenceType NodeReferenceType { get; }

    IEdge Edge { get; }

}
