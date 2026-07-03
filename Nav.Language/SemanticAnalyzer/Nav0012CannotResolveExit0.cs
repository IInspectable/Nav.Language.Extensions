using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

public class Nav0012CannotResolveExit0: NavAnalyzer {

    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0012CannotResolveExit0;

    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        // Cannot resolve exit '{0}'
        //==============================
        foreach (var exitTransition in taskDefinition.ExitTransitions) {

            if (exitTransition.ExitConnectionPointReference is { Declaration: null }) {
                yield return new Diagnostic(
                    exitTransition.ExitConnectionPointReference.Location,
                    Descriptor,
                    exitTransition.ExitConnectionPointReference.Name);

            }
        }

    }

}