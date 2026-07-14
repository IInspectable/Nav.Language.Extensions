using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

/// <summary>
/// Nav0200 (<c>Signal trigger not allowed after init</c>, Fehler): Eine Init-Transition
/// (<see cref="IInitTransition"/> — der Einstieg in den Task) darf keinen Signal-Trigger
/// (<c>on Signal</c>, <see cref="SignalTriggerSyntax"/>) tragen: Signal-Trigger lösen
/// Trigger-Transitionen aus GUI-Knoten aus (<see cref="ITriggerTransition"/>), der Übergang aus
/// einem Init-Knoten wird dagegen nicht durch ein Signal ausgelöst. z.B. meldet
/// <c>I1 --&gt; e1 on Foo;</c> hinter <c>init I1;</c> ein <c>Signal trigger not allowed after
/// init</c>. Geprüft wird die Syntax der Transition
/// (<see cref="TransitionDefinitionSyntax.Trigger"/>); die Diagnose sitzt am Trigger.
/// </summary>
public class Nav0200SignalTriggerNotAllowedAfterInit: NavAnalyzer {

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0200SignalTriggerNotAllowedAfterInit;

    /// <inheritdoc/>
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