using System.Collections.Generic;

using JetBrains.Annotations;

namespace Pharmatechnik.Nav.Language; 

public interface IEdge {

    [NotNull]
    ITaskDefinitionSymbol ContainingTask { get; }

    [NotNull]
    Location Location { get; }

    [CanBeNull]
    INodeReferenceSymbol SourceReference { get; }

    [CanBeNull]
    IEdgeModeSymbol EdgeMode { get; }

    [CanBeNull]
    INodeReferenceSymbol TargetReference { get; }

    IEnumerable<ISymbol> Symbols();

}