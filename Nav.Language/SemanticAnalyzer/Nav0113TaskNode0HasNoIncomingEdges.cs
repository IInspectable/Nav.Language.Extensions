using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

public class Nav0113TaskNode0HasNoIncomingEdges: NavAnalyzer {

    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0113TaskNode0HasNoIncomingEdges;

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