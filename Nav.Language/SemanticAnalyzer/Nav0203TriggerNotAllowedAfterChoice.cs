using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

public class Nav0203TriggerNotAllowedAfterChoice: NavAnalyzer {

    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0203TriggerNotAllowedAfterChoice;

    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        // Trigger not allowed after choice
        //==============================
        foreach (var choiceTransition in taskDefinition.ChoiceTransitions) {

            var trigger = choiceTransition.Syntax.Trigger;
            if (trigger != null) {

                yield return new Diagnostic(
                    trigger.GetLocation(),
                    Descriptor);
            }
        }
    }

}