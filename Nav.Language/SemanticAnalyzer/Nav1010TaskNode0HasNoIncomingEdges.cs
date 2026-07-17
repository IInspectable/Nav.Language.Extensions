using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

/// <summary>
/// Nav1010 (<c>The task node '{0}' has no incoming edges</c>, Dead-Code-Hinweis der Kategorie
/// <see cref="DiagnosticCategory.DeadCode"/>): Dead-Code-Gegenstück zu
/// <see cref="Nav0113TaskNode0HasNoIncomingEdges"/> — dieselbe Bedingung (ein <c>task</c>-Knoten
/// (<see cref="ITaskNodeSymbol"/>) mit ausgehenden Exit-Transitionen
/// (<see cref="ITaskNodeSymbol.Outgoings"/>), den aber selbst keine Kante erreicht
/// (<see cref="ITargetNodeSymbol.Incomings"/>)), gemeldet aber nicht an der Knoten-Deklaration,
/// sondern an den ausgehenden Exit-Transitionen selbst (die erste als Hauptfundstelle, alle
/// weiteren als Zusatz-Fundstellen) — so kann der Editor die nie durchlaufenen Transitionen
/// abgedunkelt darstellen.
/// </summary>
public class Nav1010TaskNode0HasNoIncomingEdges: NavAnalyzer {

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.DeadCode.Nav1010TaskNode0HasNoIncomingEdges;

    /// <inheritdoc/>
    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        //  The task node '{0}' has no incoming edges
        //==============================
        foreach (var taskNode in taskDefinition.NodeDeclarations.OfType<ITaskNodeSymbol>()) {

            if (taskNode.Outgoings.Any() && !taskNode.Incomings.Any()) {

                if (taskNode.Outgoings.Any()) {
                    yield return new Diagnostic(
                        taskNode.Outgoings.First().Location,
                        taskNode.Outgoings.Select(edge => edge.Location).Skip(1),
                        Descriptor,
                        taskNode.Name);
                }
            }

        }
    }

}