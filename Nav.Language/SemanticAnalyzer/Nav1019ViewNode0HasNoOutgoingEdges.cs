using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

/// <summary>
/// Nav1019 (<c>The view node '{0}' has no outgoing edges</c>, Dead-Code-Hinweis der Kategorie
/// <see cref="DiagnosticCategory.DeadCode"/>): Dead-Code-Gegenstück zu
/// <see cref="Nav0117ViewNode0HasNoOutgoingEdges"/> — dieselbe Bedingung (ein <c>view</c>-Knoten
/// (<see cref="IViewNodeSymbol"/>), den Kanten erreichen
/// (<see cref="ITargetNodeSymbol.Incomings"/>), von dem aber keine Trigger-Transition ausgeht
/// (<see cref="IGuiNodeSymbol.Outgoings"/>)), gemeldet aber nicht an der Knoten-Deklaration,
/// sondern an den eingehenden Kanten selbst (die erste Kante als Hauptfundstelle, alle weiteren
/// als Zusatz-Fundstellen) — so kann der Editor die in die Sackgasse führenden Kanten
/// abgedunkelt darstellen.
/// </summary>
public class Nav1019ViewNode0HasNoOutgoingEdges: NavAnalyzer {

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.DeadCode.Nav1019ViewNode0HasNoOutgoingEdges;

    /// <inheritdoc/>
    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        // The view node '{0}' has no outgoing edges
        //==============================
        foreach (var viewNode in taskDefinition.NodeDeclarations.OfType<IViewNodeSymbol>()) {

            if (viewNode.Incomings.Any() && !viewNode.Outgoings.Any() && !viewNode.CarriesContinuation()) {

                yield return new Diagnostic(
                    viewNode.Incomings.First().Location,
                    viewNode.Incomings.Select(edge => edge.Location).Skip(1),
                    DiagnosticDescriptors.DeadCode.Nav1019ViewNode0HasNoOutgoingEdges,
                    viewNode.Name);
            }
        }
    }

}