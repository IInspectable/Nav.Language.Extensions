using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

public class Nav0105ExitNode0MustOnlyReachedByGoTo: NavAnalyzer {

    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0105ExitNode0MustOnlyReachedByGoTo;

    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        // Exit node '{0}' can only be reached by a goto edge (-->)
        //==============================
        foreach (var transition in taskDefinition.Edges()) {

            if (transition.TargetReference?.Declaration is ExitNodeSymbol &&
                transition.EdgeMode          != null                      &&
                transition.EdgeMode.EdgeMode != EdgeMode.Goto) {

                yield return (new Diagnostic(
                    transition.EdgeMode.Location,
                    Descriptor,
                    transition.TargetReference.Name));
            }
        }
    }

}