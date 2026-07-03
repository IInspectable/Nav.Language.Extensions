using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

public class Nav0220ConditionsAreNotAllowedInTriggerTransitions: NavAnalyzer {

    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0220ConditionsAreNotAllowedInTriggerTransitions;

    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        // Conditions are not allowed in trigger transitions
        //==============================
        foreach (var triggerTransition in taskDefinition.TriggerTransitions) {

            if (triggerTransition.Syntax.ConditionClause != null) {
                yield return (new Diagnostic(
                    triggerTransition.Syntax.ConditionClause.GetLocation(),
                    Descriptor));
            }
        }
    }

}