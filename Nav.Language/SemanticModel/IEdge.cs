using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Eine Kante im Transitionsgraphen einer Task-Definition: Quellknoten-Referenz, Kantenmodus
/// (Operator) und Zielknoten-Referenz — in <c>Start --&gt; Auswahl;</c> die gesamte Zeile.
/// Gemeinsame Basis aller Kanten-Arten: der regulären Transitionen (<see cref="ITransition"/>),
/// der Exit-Transitionen (<see cref="IExitTransition"/>) und der Continuations
/// (<see cref="IContinuationTransition"/>). Eine Kante ist selbst kein <see cref="ISymbol"/>;
/// ihre Bestandteile sind es (siehe <see cref="Symbols"/>).
/// </summary>
public interface IEdge {

    /// <summary>Die Task-Definition, in deren Transitionsblock diese Kante deklariert ist.</summary>
    ITaskDefinitionSymbol ContainingTask { get; }

    /// <summary>Die Fundstelle der gesamten Kanten-Definition im Quelltext.</summary>
    Location Location { get; }

    /// <summary>
    /// Die Referenz auf den Quellknoten der Kante — <c>null</c>, wenn sie im Quelltext fehlt
    /// oder (bei <see cref="ITransition"/>) der Quellknoten nicht aufgelöst werden konnte.
    /// </summary>
    INodeReferenceSymbol? SourceReference { get; }

    /// <summary>
    /// Der Kantenmodus — das Symbol des Kanten-Operators (<c>--&gt;</c>/<c>o-&gt;</c>/<c>==&gt;</c>
    /// bzw. bei Continuations <c>--^</c>/<c>o-^</c>); <c>null</c>, wenn der Operator im Quelltext fehlt.
    /// </summary>
    IEdgeModeSymbol? EdgeMode { get; }

    /// <summary>
    /// Die Referenz auf den Zielknoten der Kante — <c>null</c>, wenn er im Quelltext fehlt.
    /// </summary>
    INodeReferenceSymbol? TargetReference { get; }

    /// <summary>
    /// Liefert die zu dieser Kante gehörenden Teil-Symbole (Quellreferenz, Kantenmodus,
    /// Zielreferenz), soweit vorhanden — konkrete Kanten-Arten ergänzen ihre Zusatz-Symbole
    /// (z.B. Trigger, Exit-Verbindungspunkt-Referenz, Continuation-Bestandteile).
    /// </summary>
    IEnumerable<ISymbol> Symbols();

}
