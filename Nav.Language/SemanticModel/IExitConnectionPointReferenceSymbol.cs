namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Symbol einer Verwendung eines <c>exit</c>-Verbindungspunkts auf der Quellseite einer
/// Exit-Transition — in <c>Auswahl:Abbrechen --&gt; end;</c> der Name <c>Abbrechen</c> hinter dem
/// Doppelpunkt. Analog zu <see cref="INodeReferenceSymbol"/> steht die Referenz für genau eine
/// Fundstelle an genau einer Kante (<see cref="ExitTransition"/>); die Deklaration ist der
/// gleichnamige Ausgang der Task-Deklaration des Quellknotens.
/// </summary>
public interface IExitConnectionPointReferenceSymbol: ISymbol {

    /// <summary>
    /// Der <c>exit</c>-Verbindungspunkt, auf den sich diese Referenz auflöst — <c>null</c>, wenn
    /// die Task-Deklaration des Quellknotens keinen Ausgang dieses Namens besitzt
    /// (Diagnose Nav0012).
    /// </summary>
    IExitConnectionPointSymbol? Declaration { get; }

    /// <summary>Die Exit-Transition, zu der diese Referenz gehört.</summary>
    IExitTransition ExitTransition { get; }

}
