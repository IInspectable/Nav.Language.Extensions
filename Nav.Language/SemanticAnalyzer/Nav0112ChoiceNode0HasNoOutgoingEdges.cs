using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

public class Nav0112ChoiceNode0HasNoOutgoingEdges: NavAnalyzer {

    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0112ChoiceNode0HasNoOutgoingEdges;

    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        //  The choice node '{0}' has no outgoing edges
        //==============================
        foreach (var choiceNode in taskDefinition.NodeDeclarations.OfType<IChoiceNodeSymbol>()) {

            if (choiceNode.Incomings.Any() && !choiceNode.Outgoings.Any()) {

                yield return new Diagnostic(
                    choiceNode.Location,
                    Descriptor,
                    choiceNode.Name);
            }
        }

    }

}