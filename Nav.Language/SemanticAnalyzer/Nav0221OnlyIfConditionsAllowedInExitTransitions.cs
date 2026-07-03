#nullable enable

using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

public class Nav0221OnlyIfConditionsAllowedInExitTransitions: NavAnalyzer {

    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0221OnlyIfConditionsAllowedInExitTransitions;

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