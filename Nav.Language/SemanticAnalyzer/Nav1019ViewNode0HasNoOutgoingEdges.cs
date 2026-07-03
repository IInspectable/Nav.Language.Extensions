using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

public class Nav1019ViewNode0HasNoOutgoingEdges: NavAnalyzer {

    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.DeadCode.Nav1019ViewNode0HasNoOutgoingEdges;

    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        // The view node '{0}' has no outgoing edges
        //==============================
        foreach (var viewNode in taskDefinition.NodeDeclarations.OfType<IViewNodeSymbol>()) {

            if (viewNode.Incomings.Any() && !viewNode.Outgoings.Any()) {

                yield return new Diagnostic(
                    viewNode.Incomings.First().Location,
                    viewNode.Incomings.Select(edge => edge.Location).Skip(1),
                    DiagnosticDescriptors.DeadCode.Nav1019ViewNode0HasNoOutgoingEdges,
                    viewNode.Name);
            }
        }
    }

}