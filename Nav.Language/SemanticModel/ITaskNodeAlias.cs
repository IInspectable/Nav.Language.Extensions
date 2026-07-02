using JetBrains.Annotations;

namespace Pharmatechnik.Nav.Language; 

public interface ITaskNodeAliasSymbol: ISymbol {

    [NotNull]
    ITaskNodeSymbol TaskNode { get; }

}