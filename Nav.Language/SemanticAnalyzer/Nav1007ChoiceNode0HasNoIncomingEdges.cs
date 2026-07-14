using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

/// <summary>
/// Nav1007 (<c>The choice node '{0}' has no incoming edges</c>, Dead-Code-Hinweis der Kategorie
/// <see cref="DiagnosticCategory.DeadCode"/>): Dead-Code-Gegenstück zu
/// <see cref="Nav0111ChoiceNode0HasNoIncomingEdges"/> — dieselbe Bedingung (ein
/// <c>choice</c>-Knoten (<see cref="IChoiceNodeSymbol"/>) mit ausgehenden Kanten
/// (<see cref="IChoiceNodeSymbol.Outgoings"/>), den aber selbst keine Kante erreicht
/// (<see cref="ITargetNodeSymbol.Incomings"/>)), gemeldet aber nicht an der Knoten-Deklaration,
/// sondern an den ausgehenden Kanten selbst (die erste Kante als Hauptfundstelle, alle weiteren
/// als Zusatz-Fundstellen) — so kann der Editor die nie durchlaufenen Kanten abgedunkelt
/// darstellen.
/// </summary>
public class Nav1007ChoiceNode0HasNoIncomingEdges: NavAnalyzer {

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.DeadCode.Nav1007ChoiceNode0HasNoIncomingEdges;

    /// <inheritdoc/>
    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        //  The choice node '{0}' has no incoming edges
        //==============================
        foreach (var choiceNode in taskDefinition.NodeDeclarations.OfType<IChoiceNodeSymbol>()) {

            if (choiceNode.Outgoings.Any() && !choiceNode.Incomings.Any()) {

                yield return new Diagnostic(
                    choiceNode.Outgoings.First().Location,
                    choiceNode.Outgoings.Select(edge => edge.Location).Skip(1),
                    Descriptor,
                    choiceNode.Name);
            }
        }

    }

}