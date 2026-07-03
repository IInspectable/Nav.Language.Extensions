using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

public class Nav0111ChoiceNode0HasNoIncomingEdges: NavAnalyzer {

    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0111ChoiceNode0HasNoIncomingEdges;

    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        //  The choice node '{0}' has no incoming edges
        //==============================
        foreach (var choiceNode in taskDefinition.NodeDeclarations.OfType<IChoiceNodeSymbol>()) {

            if (choiceNode.Outgoings.Any() && !choiceNode.Incomings.Any()) {

                yield return new Diagnostic(
                    choiceNode.Location,
                    Descriptor,
                    choiceNode.Name);
            }
        }

    }

}