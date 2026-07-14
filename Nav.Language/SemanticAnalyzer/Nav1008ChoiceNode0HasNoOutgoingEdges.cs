using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

/// <summary>
/// Nav1008 (<c>The choice node '{0}' has no outgoing edges</c>, Dead-Code-Hinweis der Kategorie
/// <see cref="DiagnosticCategory.DeadCode"/>): Dead-Code-Gegenstück zu
/// <see cref="Nav0112ChoiceNode0HasNoOutgoingEdges"/> — dieselbe Bedingung (ein
/// <c>choice</c>-Knoten (<see cref="IChoiceNodeSymbol"/>), den Kanten erreichen
/// (<see cref="ITargetNodeSymbol.Incomings"/>), der aber keine ausgehende Kante besitzt
/// (<see cref="IChoiceNodeSymbol.Outgoings"/>)), gemeldet aber nicht an der Knoten-Deklaration,
/// sondern an den eingehenden Kanten selbst (die erste Kante als Hauptfundstelle, alle weiteren
/// als Zusatz-Fundstellen) — so kann der Editor die in die Sackgasse führenden Kanten
/// abgedunkelt darstellen.
/// </summary>
public class Nav1008ChoiceNode0HasNoOutgoingEdges: NavAnalyzer {

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.DeadCode.Nav1008ChoiceNode0HasNoOutgoingEdges;

    /// <inheritdoc/>
    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        //  The choice node '{0}' has no outgoing edges
        //==============================
        foreach (var choiceNode in taskDefinition.NodeDeclarations.OfType<IChoiceNodeSymbol>()) {

            if (choiceNode.Incomings.Any() && !choiceNode.Outgoings.Any()) {

                yield return new Diagnostic(
                    choiceNode.Incomings.First().Location,
                    choiceNode.Incomings.Select(edge => edge.Location).Skip(1),
                    Descriptor,
                    choiceNode.Name);
            }
        }

    }

}