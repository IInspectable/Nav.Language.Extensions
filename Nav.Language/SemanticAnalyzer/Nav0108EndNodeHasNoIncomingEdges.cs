using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

public class Nav0108EndNodeHasNoIncomingEdges: NavAnalyzer {

    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0108EndNodeHasNoIncomingEdges;

    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        //  The end node has no incoming edges
        //==============================
        foreach (var endNode in taskDefinition.NodeDeclarations.OfType<IEndNodeSymbol>()) {
               
            if (!endNode.Incomings.Any()) {

                yield return new Diagnostic(
                    endNode.Location,
                    Descriptor,
                    endNode.Name);
            }
        }
    }

}