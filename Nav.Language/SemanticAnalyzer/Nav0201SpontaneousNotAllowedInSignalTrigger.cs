using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

public class Nav0201SpontaneousNotAllowedInSignalTrigger: NavAnalyzer {

    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0201SpontaneousNotAllowedInSignalTrigger;

    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        // Spontaneous not allowed in signal trigger
        //==============================
        foreach (var triggerTransition in taskDefinition.TriggerTransitions) {

            foreach (var trigger in triggerTransition.Triggers.Where(t => t.IsSignalTrigger)) {

                if (trigger.Name == SyntaxFacts.SpontaneousKeyword || trigger.Name == SyntaxFacts.SpontKeyword) {
                    yield return new Diagnostic(
                        trigger.Location,
                        Descriptor);
                }
            }
        }
    }

}