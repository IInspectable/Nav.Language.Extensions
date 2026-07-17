using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

/// <summary>
/// Nav0117 (<c>The view node '{0}' has no outgoing edges</c>, Warnung): Ein <c>view</c>-Knoten
/// (<see cref="IViewNodeSymbol"/>), den Kanten erreichen
/// (<see cref="ITargetNodeSymbol.Incomings"/>), von dem aber keine Trigger-Transition ausgeht
/// (<see cref="IGuiNodeSymbol.Outgoings"/>), ist eine Sackgasse — aus der angezeigten View führt
/// kein Weg weiter, z.B. <c>I2 --&gt; C;</c> ohne eine Transition <b>von</b> <c>C</c> weg. Gemeldet
/// wird an der Knoten-Deklaration; ein View-Knoten ganz ohne Kanten bleibt hier unbeanstandet, ebenso
/// ein View, der eine Continuation trägt (<see cref="GuiNodeSymbolExtensions.CarriesContinuation"/>) —
/// der Ablauf läuft dort in den Folge-Task weiter. Das
/// Dead-Code-Gegenstück <see cref="Nav1019ViewNode0HasNoOutgoingEdges"/> meldet unter derselben
/// Bedingung an den eingehenden Kanten selbst.
/// </summary>
public class Nav0117ViewNode0HasNoOutgoingEdges: NavAnalyzer {

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0117ViewNode0HasNoOutgoingEdges;

    /// <inheritdoc/>
    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        // The view node '{0}' has no outgoing edges
        //==============================
        foreach (var viewNode in taskDefinition.NodeDeclarations.OfType<IViewNodeSymbol>()) {

            if (viewNode.Incomings.Any() && !viewNode.Outgoings.Any() && !viewNode.CarriesContinuation()) {

                yield return new Diagnostic(
                    viewNode.Location,
                    Descriptor,
                    viewNode.Name);
            }
        }
    }

}