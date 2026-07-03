using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

public class Nav0011CannotResolveNode0: NavAnalyzer {

    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0011CannotResolveNode0;

    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        // Cannot resolve node '{0}'
        //==============================
        foreach (var targetReference in taskDefinition.Edges().Select(e => e.TargetReference)) {

            if (targetReference is { Declaration: null }) {
                yield return new Diagnostic(
                    targetReference.Location,
                    Descriptor,
                    targetReference.Name);
            }
        }
    }

}