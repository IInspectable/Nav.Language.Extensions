namespace Pharmatechnik.Nav.Language;

public interface IExitConnectionPointReferenceSymbol: ISymbol {

    IExitConnectionPointSymbol? Declaration { get; }

    IExitTransition ExitTransition { get; }

}
