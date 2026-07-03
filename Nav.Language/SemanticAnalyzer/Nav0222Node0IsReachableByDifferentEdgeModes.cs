using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

public class Nav0222Node0IsReachableByDifferentEdgeModes: NavAnalyzer {

    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0222Node0IsReachableByDifferentEdgeModes;

    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        // Node '{0}' is reached by edges of different modes
        //==============================
        foreach (IEdge edge in taskDefinition.Edges()) {

            foreach (var nodeCalls in edge.GetReachableCalls().GroupBy(c => c.Node)) {

                if (nodeCalls.GroupBy(c => c.EdgeMode.EdgeMode).Count() > 1) {

                    yield return new Diagnostic(
                        nodeCalls.First().EdgeMode.Location,
                        nodeCalls.Skip(1).Select(call => call.EdgeMode.Location),
                        Descriptor,
                        nodeCalls.Key.Name
                    );
                }
            }

        }
    }

}