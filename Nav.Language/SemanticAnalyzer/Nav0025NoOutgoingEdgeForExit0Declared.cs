using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

public class Nav0025NoOutgoingEdgeForExit0Declared: NavAnalyzer {

    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0025NoOutgoingEdgeForExit0Declared;

    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        //  No outgoing edge declared for exit '{0}'
        //==============================
        foreach (var taskNode in taskDefinition.NodeDeclarations.OfType<ITaskNodeSymbol>()) {

            // Wird mit Nav1012TaskNode0NotRequired behandelt
            if (!taskNode.References.Any()) {
                continue;
            }

            foreach (var expectedExit in taskNode.GetUnconnectedExits()) {

                yield return new Diagnostic(
                    taskNode.Location,
                    taskNode.Incomings
                            .Select(edge => edge.TargetReference)
                            .WhereNotNull()
                            .Select(nodeReference => nodeReference.Location),
                    Descriptor,
                    expectedExit.Name);
            }
        }

    }

}