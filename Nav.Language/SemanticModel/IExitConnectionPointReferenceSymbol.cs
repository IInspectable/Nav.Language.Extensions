using JetBrains.Annotations;

namespace Pharmatechnik.Nav.Language; 

public interface IExitConnectionPointReferenceSymbol: ISymbol {

    [CanBeNull]
    IExitConnectionPointSymbol Declaration { get; }

    [NotNull]
    IExitTransition ExitTransition { get; }

}