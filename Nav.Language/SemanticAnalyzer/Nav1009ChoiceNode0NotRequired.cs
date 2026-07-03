using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

public class Nav1009ChoiceNode0NotRequired: NavAnalyzer {

    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.DeadCode.Nav1009ChoiceNode0NotRequired;

    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        //  The choice node '{0}' is not required by the code and can be safely removed
        //==============================
        foreach (var choiceNode in taskDefinition.NodeDeclarations.OfType<IChoiceNodeSymbol>()) {

            if (!choiceNode.References.Any()) {

                yield return new Diagnostic(
                    choiceNode.Syntax.GetLocation(),
                    Descriptor,
                    choiceNode.Name);
            }

        }

    }

}