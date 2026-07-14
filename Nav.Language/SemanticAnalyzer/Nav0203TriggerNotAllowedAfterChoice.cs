using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

/// <summary>
/// Nav0203 (<c>Trigger not allowed after choice</c>, Fehler): Eine Choice-Transition
/// (<see cref="IChoiceTransition"/> — der Quellknoten ist ein <c>choice</c>) darf keinen Trigger
/// tragen, weder <c>on Signal</c> noch <c>spontaneous</c>: eine Choice verzweigt anhand von
/// Bedingungen (<see cref="IChoiceNodeSymbol"/>), nicht anhand von Signalen. z.B. meldet
/// <c>C --&gt; e1 on Foo;</c> hinter <c>choice C;</c> ein <c>Trigger not allowed after choice</c>.
/// Geprüft wird die Syntax der Transition (<see cref="TransitionDefinitionSyntax.Trigger"/>); die
/// Diagnose sitzt am Trigger.
/// </summary>
public class Nav0203TriggerNotAllowedAfterChoice: NavAnalyzer {

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0203TriggerNotAllowedAfterChoice;

    /// <inheritdoc/>
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