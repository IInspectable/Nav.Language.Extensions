using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language;

public interface IEdge {

    ITaskDefinitionSymbol ContainingTask { get; }

    Location Location { get; }

    INodeReferenceSymbol? SourceReference { get; }

    IEdgeModeSymbol? EdgeMode { get; }

    INodeReferenceSymbol? TargetReference { get; }

    IEnumerable<ISymbol> Symbols();

}
