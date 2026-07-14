using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

/// <summary>
/// Nav1016 (<c>The dialog node '{0}' has no outgoing edges</c>, Dead-Code-Hinweis der Kategorie
/// <see cref="DiagnosticCategory.DeadCode"/>): Dead-Code-Gegenstück zu
/// <see cref="Nav0115DialogNode0HasNoOutgoingEdges"/> — dieselbe Bedingung (ein
/// <c>dialog</c>-Knoten (<see cref="IDialogNodeSymbol"/>), den Kanten erreichen
/// (<see cref="ITargetNodeSymbol.Incomings"/>), von dem aber keine Trigger-Transition ausgeht
/// (<see cref="IGuiNodeSymbol.Outgoings"/>)), gemeldet aber nicht an der Knoten-Deklaration,
/// sondern an den eingehenden Kanten selbst (die erste Kante als Hauptfundstelle, alle weiteren
/// als Zusatz-Fundstellen) — so kann der Editor die in die Sackgasse führenden Kanten
/// abgedunkelt darstellen.
/// </summary>
public class Nav1016DialogNode0HasNoOutgoingEdges: NavAnalyzer {

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.DeadCode.Nav1016DialogNode0HasNoOutgoingEdges;

    /// <inheritdoc/>
    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        // The dialog node '{0}' has no outgoing edges
        //==============================
        foreach (var dialogNode in taskDefinition.NodeDeclarations.OfType<IDialogNodeSymbol>()) {

            if (dialogNode.Incomings.Any() && !dialogNode.Outgoings.Any()) {

                yield return new Diagnostic(
                    dialogNode.Incomings.First().Location,
                    dialogNode.Incomings.Select(edge => edge.Location).Skip(1),
                    Descriptor,
                    dialogNode.Name);
            }
        }
    }

}