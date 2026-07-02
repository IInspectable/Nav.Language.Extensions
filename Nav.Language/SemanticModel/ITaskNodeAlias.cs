#nullable enable

namespace Pharmatechnik.Nav.Language;

public interface ITaskNodeAliasSymbol: ISymbol {

    ITaskNodeSymbol TaskNode { get; }

}
