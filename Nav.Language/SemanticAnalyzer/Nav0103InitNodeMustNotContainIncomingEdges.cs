using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

public class Nav0103InitNodeMustNotContainIncomingEdges: NavAnalyzer {

    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0103InitNodeMustNotContainIncomingEdges;

    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        // An init node must not contain incoming edges
        //==============================
        foreach (var targetReference in taskDefinition.Edges().Select(e => e.TargetReference)) {

            if (targetReference?.Declaration is IInitNodeSymbol) {
                yield return (new Diagnostic(
                    targetReference.Location,
                    Descriptor,
                    targetReference.Name));
            }
        }
    }

}