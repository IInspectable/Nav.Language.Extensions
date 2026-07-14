#region Using Directives

using System.Linq;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>Erweiterungen auf <see cref="IGuiNodeSymbol"/>.</summary>
public static class GuiNodeSymbolExtensions {

    /// <summary>
    /// Gibt an, ob dieser GUI-Knoten der <b>tragende Knoten</b> einer Continuation ist
    /// (<c>… --&gt; {Knoten} o-^ Task</c> bzw. <c>… --&gt; {Knoten} --^ Task</c>, ab Sprachversion 2).
    /// Die Continuation hängt am Zielknoten der eingehenden Transition — also an diesem Knoten —, ist
    /// aber bewusst <b>keine</b> ausgehende Trigger-Transition (<see cref="IGuiNodeSymbol.Outgoings"/>),
    /// sondern als eingehende Kante ihres Folge-Tasks verdrahtet. Sie zählt daher nicht zu
    /// <see cref="IGuiNodeSymbol.Outgoings"/>, führt den Ablauf ab der angezeigten View/dem Dialog aber
    /// sehr wohl weiter — weshalb die Sackgassen-Analyzer (Nav0115/Nav0117 samt Dead-Code-Gegenstücken
    /// Nav1016/Nav1019) einen Continuation-Träger nicht als Sackgasse melden.
    /// </summary>
    /// <param name="guiNode">Der GUI-Knoten (View oder Dialog) oder <c>null</c>.</param>
    public static bool CarriesContinuation(this IGuiNodeSymbol? guiNode) {
        return guiNode != null &&
               guiNode.Incomings
                      .OfType<IContinuableEdge>()
                      .Any(edge => edge.ContinuationTransition != null);
    }

}
