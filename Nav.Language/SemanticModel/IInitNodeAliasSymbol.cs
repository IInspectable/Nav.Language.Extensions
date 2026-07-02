using JetBrains.Annotations;

namespace Pharmatechnik.Nav.Language; 

public interface IInitNodeAliasSymbol: ISymbol {

    [NotNull]
    IInitNodeSymbol InitNode { get; }

}