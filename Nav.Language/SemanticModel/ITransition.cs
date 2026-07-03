namespace Pharmatechnik.Nav.Language;

public interface ITransition: IEdge {

    TransitionDefinitionSyntax Syntax { get; }

}

public interface IInitTransition: ITransition {

    IInitNodeReferenceSymbol? InitNodeSourceReference { get; }

}

public interface ITriggerTransition: ITransition {

    IGuiNodeReferenceSymbol? GuiNodeSourceReference { get; }

    IReadOnlySymbolCollection<ITriggerSymbol> Triggers { get; }

}

public interface IChoiceTransition: ITransition {

    IChoiceNodeReferenceSymbol? ChoiceNodeSourceReference { get; }

}

public interface IExitTransition: IEdge {

    ExitTransitionDefinitionSyntax Syntax { get; }

    ITaskNodeReferenceSymbol? TaskNodeSourceReference { get; }

    IExitConnectionPointReferenceSymbol? ExitConnectionPointReference { get; }

}
