using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

public class Nav0200SignalTriggerNotAllowedAfterInit: NavAnalyzer {

    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0200SignalTriggerNotAllowedAfterInit;

    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        // Signal trigger not allowed after init
        //==============================
        foreach (var initTransition in taskDefinition.InitTransitions) {

            if (initTransition.Syntax.Trigger is SignalTriggerSyntax trigger) {

                yield return new Diagnostic(
                    trigger.GetLocation(),
                    Descriptor);
            }
        }
    }

}