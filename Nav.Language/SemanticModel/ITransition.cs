using JetBrains.Annotations;

namespace Pharmatechnik.Nav.Language; 

public interface ITransition: IEdge {

    [NotNull]
    TransitionDefinitionSyntax Syntax { get; }

}

public interface IInitTransition: ITransition {

    [CanBeNull]
    IInitNodeReferenceSymbol InitNodeSourceReference { get; }

}

public interface ITriggerTransition: ITransition {

    [CanBeNull]
    IGuiNodeReferenceSymbol GuiNodeSourceReference { get; }

    [NotNull]
    IReadOnlySymbolCollection<ITriggerSymbol> Triggers { get; }

}

public interface IChoiceTransition: ITransition {

    [CanBeNull]
    IChoiceNodeReferenceSymbol ChoiceNodeSourceReference { get; }

}

public interface IExitTransition: IEdge {

    [NotNull]
    ExitTransitionDefinitionSyntax Syntax { get; }

    [CanBeNull]
    ITaskNodeReferenceSymbol TaskNodeSourceReference { get; }

    [CanBeNull]
    IExitConnectionPointReferenceSymbol ExitConnectionPointReference { get; }

}