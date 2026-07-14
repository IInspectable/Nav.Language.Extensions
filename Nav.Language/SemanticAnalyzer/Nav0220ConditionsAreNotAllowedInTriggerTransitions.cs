using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

/// <summary>
/// Nav0220 (<c>Conditions are not allowed in trigger transitions</c>, Fehler): Eine
/// Trigger-Transition (<see cref="ITriggerTransition"/> — der Quellknoten ist ein GUI-Knoten) darf
/// keine Bedingungs-Klausel (<see cref="ConditionClauseSyntax"/>: <c>if</c>/<c>else</c>/<c>else
/// if</c>) tragen — Bedingungen verzweigen hinter Init- und Choice-Knoten (bzw. eingeschränkt
/// hinter Exits, <see cref="Nav0221OnlyIfConditionsAllowedInExitTransitions"/>). z.B. meldet
/// <c>V1 --&gt; e1 on Foo if Bla;</c> ein <c>Conditions are not allowed in trigger transitions</c>.
/// Die Diagnose sitzt an der Bedingungs-Klausel
/// (<see cref="TransitionDefinitionSyntax.ConditionClause"/>).
/// </summary>
public class Nav0220ConditionsAreNotAllowedInTriggerTransitions: NavAnalyzer {

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0220ConditionsAreNotAllowedInTriggerTransitions;

    /// <inheritdoc/>
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