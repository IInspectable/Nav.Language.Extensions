using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

/// <summary>
/// Nav0113 (<c>The task node '{0}' has no incoming edges</c>, Warnung): Ein <c>task</c>-Knoten
/// (<see cref="ITaskNodeSymbol"/>), von dem Exit-Transitionen ausgehen
/// (<see cref="ITaskNodeSymbol.Outgoings"/>), den aber selbst keine Kante erreicht
/// (<see cref="ITargetNodeSymbol.Incomings"/>), ist unerreichbar — der eingebundene Task wird nie
/// aufgerufen, z.B. <c>task A; A:e1 --&gt; e1;</c> ohne eine Kante <b>auf</b> <c>A</c>. Gemeldet
/// wird an der Knoten-Deklaration; ein Task-Knoten ganz ohne Kanten bleibt hier unbeanstandet. Das
/// Dead-Code-Gegenstück <see cref="Nav1010TaskNode0HasNoIncomingEdges"/> meldet unter derselben
/// Bedingung an den ausgehenden Exit-Transitionen selbst.
/// </summary>
public class Nav0113TaskNode0HasNoIncomingEdges: NavAnalyzer {

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0113TaskNode0HasNoIncomingEdges;

    /// <inheritdoc/>
    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        //  The task node '{0}' has no incoming edges
        //==============================
        foreach (var taskNode in taskDefinition.NodeDeclarations.OfType<ITaskNodeSymbol>()) {

            if (taskNode.Outgoings.Any() && !taskNode.Incomings.Any()) {

                yield return new Diagnostic(
                    taskNode.Location,
                    Descriptor,
                    taskNode.Name);

            }

        }
    }

}