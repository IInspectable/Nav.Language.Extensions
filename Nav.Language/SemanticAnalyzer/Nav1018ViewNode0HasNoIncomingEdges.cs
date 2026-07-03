using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

public class Nav1018ViewNode0HasNoIncomingEdges: NavAnalyzer {

    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.DeadCode.Nav1018ViewNode0HasNoIncomingEdges;

    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        // The view node '{0}' has no incoming edges
        //==============================
        foreach (var viewNode in taskDefinition.NodeDeclarations.OfType<IViewNodeSymbol>()) {

            if (viewNode.Outgoings.Any() && !viewNode.Incomings.Any()) {

                yield return (new Diagnostic(
                    viewNode.Outgoings.First().Location,
                    viewNode.Outgoings.Select(edge => edge.Location).Skip(1),
                    Descriptor,
                    viewNode.Name));
            }
        }
    }

}