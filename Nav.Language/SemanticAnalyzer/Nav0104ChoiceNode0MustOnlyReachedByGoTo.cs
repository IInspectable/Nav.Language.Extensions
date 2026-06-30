using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

public class Nav0104ChoiceNode0MustOnlyReachedByGoTo: NavAnalyzer {

    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0104ChoiceNode0MustOnlyReachedByGoTo;

    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        // Choice node '{0}' can only be reached by a goto edge (-->)
        //==============================
        foreach (var transition in taskDefinition.Edges()) {

            if (transition.TargetReference?.Declaration is ChoiceNodeSymbol &&
                transition.EdgeMode          != null                        &&
                transition.EdgeMode.EdgeMode != EdgeMode.Goto) {

                yield return (new Diagnostic(
                    transition.EdgeMode.Location,
                    Descriptor,
                    transition.TargetReference.Name));
            }
        }
    }

}