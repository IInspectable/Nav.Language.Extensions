#nullable enable

namespace Pharmatechnik.Nav.Language;

public interface IInitNodeAliasSymbol: ISymbol {

    IInitNodeSymbol InitNode { get; }

}
