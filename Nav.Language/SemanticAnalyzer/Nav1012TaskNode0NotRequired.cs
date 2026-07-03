using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer;

public class Nav1012TaskNode0NotRequired: NavAnalyzer {

    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.DeadCode.Nav1012TaskNode0NotRequired;

    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        //  The task node '{0}' is not required by the code and can be safely removed
        //==============================
        foreach (var taskNode in taskDefinition.NodeDeclarations.OfType<ITaskNodeSymbol>()) {

            if (context.IsWarningDisabled(taskNode, Descriptor)) {
                continue;
            }

            if (!taskNode.References.Any()) {

                yield return new Diagnostic(
                    taskNode.Syntax.GetLocation(),
                    Descriptor,
                    taskNode.Name);
            }
        }

    }

}