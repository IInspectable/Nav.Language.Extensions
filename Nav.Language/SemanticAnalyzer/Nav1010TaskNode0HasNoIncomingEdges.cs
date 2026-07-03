using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

public class Nav1010TaskNode0HasNoIncomingEdges: NavAnalyzer {

    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.DeadCode.Nav1010TaskNode0HasNoIncomingEdges;

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