namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Eine Kante, die einen <see cref="ContinuationTransition"/>-Anhang tragen <b>kann</b> (ab Sprachversion 2):
/// der tragende GUI-Knoten zeigt eine View <b>und</b> setzt den Übergang in einen Folge-Task fort
/// (<c>… o-^ Task</c> bzw. <c>… --^ Task</c>). Ohne Continuation ist <see cref="ContinuationTransition"/> null.
/// </summary>
public interface IContinuableEdge: IEdge {

    IContinuationTransition? ContinuationTransition { get; }

}

public interface ITransition: IContinuableEdge {

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

public interface IExitTransition: IContinuableEdge {

    ExitTransitionDefinitionSyntax Syntax { get; }

    ITaskNodeReferenceSymbol? TaskNodeSourceReference { get; }

    IExitConnectionPointReferenceSymbol? ExitConnectionPointReference { get; }

}

/// <summary>
/// Der Fortsetzungs-Anhang einer <see cref="IContinuableEdge"/> (ab Sprachversion 2): eine eigene Kante,
/// die selbst keine weitere Continuation tragen kann (daher <see cref="IEdge"/>, nicht
/// <see cref="IContinuableEdge"/>). Quelle ist der tragende GUI-Knoten der umgebenden Transition,
/// Ziel der Folge-Task; <see cref="IEdge.EdgeMode"/> bestimmt die Fortsetzungs-Art (<c>o-^</c> → Modal,
/// <c>--^</c> → Goto).
/// </summary>
public interface IContinuationTransition: IEdge {

    ContinuationTransitionSyntax Syntax { get; }

}
