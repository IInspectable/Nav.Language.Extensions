namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Eine Kante, die einen <see cref="ContinuationTransition"/>-Anhang tragen <b>kann</b> (ab Sprachversion 2):
/// der tragende GUI-Knoten zeigt eine View <b>und</b> setzt die Transition in einen Folge-Task fort
/// (<c>… o-^ Task</c> bzw. <c>… --^ Task</c>). Ohne Continuation ist <see cref="ContinuationTransition"/> null.
/// </summary>
public interface IContinuableEdge: IEdge {

    /// <summary>
    /// Der Continuation-Anhang dieser Kante (<c>… o-^ Task</c> / <c>… --^ Task</c>) —
    /// <c>null</c>, wenn die Kante keine Continuation trägt.
    /// </summary>
    IContinuationTransition? ContinuationTransition { get; }

}

/// <summary>
/// Eine reguläre Transition — eine Kante des Transitionsblocks, die von einer
/// <see cref="TransitionDefinitionSyntax"/> stammt. Die Art des <b>Quellknotens</b> bestimmt die
/// konkrete Ausprägung: <c>init</c> → <see cref="IInitTransition"/>, Choice-Knoten →
/// <see cref="IChoiceTransition"/>, GUI-Knoten (View/Dialog) → <see cref="ITriggerTransition"/>.
/// Task-Knoten haben keine Transitionen — ihre Fortsetzung läuft über <see cref="IExitTransition"/>;
/// Exit- und End-Knoten dürfen gar keine ausgehenden Kanten haben (Nav0100–Nav0102).
/// </summary>
public interface ITransition: IContinuableEdge {

    /// <summary>Der Syntaxknoten, aus dem diese Transition entstanden ist.</summary>
    TransitionDefinitionSyntax Syntax { get; }

}

/// <summary>
/// Eine Transition, deren Quelle ein Init-Knoten ist — der Einstieg in den Task, z.B.
/// <c>init --&gt; Start;</c>. Gesammelt in <see cref="ITaskDefinitionSymbol.InitTransitions"/>.
/// </summary>
public interface IInitTransition: ITransition {

    /// <summary>
    /// Die Quellreferenz als typisierte Init-Knoten-Referenz — dieselbe Instanz wie
    /// <see cref="IEdge.SourceReference"/>.
    /// </summary>
    IInitNodeReferenceSymbol? InitNodeSourceReference { get; }

}

/// <summary>
/// Eine Transition, deren Quelle ein GUI-Knoten (View/Dialog) ist und die von
/// <see cref="Triggers"/> ausgelöst wird, z.B. <c>View --&gt; Ziel on Speichern;</c>.
/// Gesammelt in <see cref="ITaskDefinitionSymbol.TriggerTransitions"/>.
/// </summary>
public interface ITriggerTransition: ITransition {

    /// <summary>
    /// Die Quellreferenz als typisierte GUI-Knoten-Referenz — dieselbe Instanz wie
    /// <see cref="IEdge.SourceReference"/>.
    /// </summary>
    IGuiNodeReferenceSymbol? GuiNodeSourceReference { get; }

    /// <summary>
    /// Die Trigger dieser Transition — Signal-Trigger (<c>on Signal</c>) oder spontane Transitionen
    /// (<c>spontaneous</c>/<c>spont</c>); leer, wenn der Quelltext keinen Trigger angibt.
    /// </summary>
    IReadOnlySymbolCollection<ITriggerSymbol> Triggers { get; }

}

/// <summary>
/// Eine Transition, deren Quelle ein Choice-Knoten ist — ein bedingter Verzweigungsausgang, z.B.
/// <c>Auswahl --&gt; Ziel if Bedingung;</c>. Gesammelt in
/// <see cref="ITaskDefinitionSymbol.ChoiceTransitions"/>.
/// </summary>
public interface IChoiceTransition: ITransition {

    /// <summary>
    /// Die Quellreferenz als typisierte Choice-Knoten-Referenz — dieselbe Instanz wie
    /// <see cref="IEdge.SourceReference"/>.
    /// </summary>
    IChoiceNodeReferenceSymbol? ChoiceNodeSourceReference { get; }

}

/// <summary>
/// Eine Exit-Transition, z.B. <c>TaskKnoten:Abbrechen --&gt; end;</c> — verdrahtet einen benannten
/// Exit-Verbindungspunkt eines eingebetteten Task-Knotens mit einem Zielknoten
/// (<see cref="ExitTransitionDefinitionSyntax"/>). Die Quelle ist stets ein Task-Knoten; anders als
/// eine <see cref="ITransition"/> trägt sie keinen Trigger. Gesammelt in
/// <see cref="ITaskDefinitionSymbol.ExitTransitions"/>.
/// </summary>
public interface IExitTransition: IContinuableEdge {

    /// <summary>Der Syntaxknoten, aus dem diese Exit-Transition entstanden ist.</summary>
    ExitTransitionDefinitionSyntax Syntax { get; }

    /// <summary>
    /// Die Quellreferenz als typisierte Task-Knoten-Referenz — dieselbe Instanz wie
    /// <see cref="IEdge.SourceReference"/>.
    /// </summary>
    ITaskNodeReferenceSymbol? TaskNodeSourceReference { get; }

    /// <summary>
    /// Die Referenz auf den benannten Exit-Verbindungspunkt hinter dem <c>:</c> — <c>null</c>, wenn
    /// der Exit-Name im Quelltext fehlt oder schon der Task-Knoten selbst nicht auflösbar ist; ein
    /// unauflösbarer Exit-<b>Name</b> ergibt dagegen eine Referenz ohne
    /// <see cref="IExitConnectionPointReferenceSymbol.Declaration"/>.
    /// </summary>
    IExitConnectionPointReferenceSymbol? ExitConnectionPointReference { get; }

}

/// <summary>
/// Der Continuation-Anhang einer <see cref="IContinuableEdge"/> (ab Sprachversion 2): eine eigene Kante,
/// die selbst keine weitere Continuation tragen kann (daher <see cref="IEdge"/>, nicht
/// <see cref="IContinuableEdge"/>). Quelle ist der tragende GUI-Knoten der umgebenden Transition,
/// Ziel der Folge-Task; <see cref="IEdge.EdgeMode"/> bestimmt den Fortsetzungs-Kantenmodus (<c>o-^</c> → Modal,
/// <c>--^</c> → Goto).
/// </summary>
public interface IContinuationTransition: IEdge {

    /// <summary>Der Syntaxknoten, aus dem diese Continuation entstanden ist.</summary>
    ContinuationTransitionSyntax Syntax { get; }

}
