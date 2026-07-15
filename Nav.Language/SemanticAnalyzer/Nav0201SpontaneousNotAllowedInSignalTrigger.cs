using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

/// <summary>
/// Nav0201 (<c>Spontaneous not allowed in signal trigger</c>, Fehler): Die reservierten
/// Trigger-Namen <c>spontaneous</c>/<c>spont</c> (<see cref="SyntaxFacts.SpontaneousKeyword"/> /
/// <see cref="SyntaxFacts.SpontKeyword"/>) dürfen nicht als Signal-Name eines Signal-Triggers
/// auftreten — eine spontane Transition wird als eigener Trigger geschrieben
/// (<see cref="SpontaneousTriggerSyntax"/>), nicht als Signal hinter <c>on</c>. Geprüft werden
/// alle Signal-Trigger (<see cref="ITriggerSymbol.IsSignalTrigger"/>) der Trigger-Transitionen
/// (<see cref="ITaskDefinitionSymbol.TriggerTransitions"/>) per Namensvergleich; die Diagnose
/// sitzt am Trigger-Symbol.
/// </summary>
public class Nav0201SpontaneousNotAllowedInSignalTrigger: NavAnalyzer {

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0201SpontaneousNotAllowedInSignalTrigger;

    /// <inheritdoc/>
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