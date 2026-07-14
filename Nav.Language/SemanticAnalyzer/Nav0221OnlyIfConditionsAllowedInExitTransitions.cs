using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

/// <summary>
/// Nav0221 (<c>Only 'if' conditions are allowed in exit transitions</c>, Fehler): Eine
/// Exit-Transition (<see cref="IExitTransition"/>) darf als Bedingung nur die reine <c>if</c>-Form
/// (<see cref="IfConditionClauseSyntax"/>) tragen; <c>else</c> und <c>else if</c>
/// (<see cref="ElseConditionClauseSyntax"/>/<see cref="ElseIfConditionClauseSyntax"/>) sind
/// unzulässig. z.B. ist <c>A:e1 --&gt; e1 if "Foo";</c> erlaubt, während <c>A:e1 --&gt; e1 else;</c>
/// ein <c>Only 'if' conditions are allowed in exit transitions</c> meldet. Die Diagnose sitzt an
/// der Bedingungs-Klausel (<see cref="ExitTransitionDefinitionSyntax.ConditionClause"/>).
/// </summary>
public class Nav0221OnlyIfConditionsAllowedInExitTransitions: NavAnalyzer {

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0221OnlyIfConditionsAllowedInExitTransitions;

    /// <inheritdoc/>
    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        // Only 'if' conditions are allowed in exit transitions
        //==============================
        foreach (var exitTransition in taskDefinition.ExitTransitions) {

            if (exitTransition.Syntax.ConditionClause != null && !(exitTransition.Syntax.ConditionClause is IfConditionClauseSyntax)) {

                yield return (new Diagnostic(
                    exitTransition.Syntax.ConditionClause.GetLocation(),
                    Descriptor));
            }
        }

    }

}