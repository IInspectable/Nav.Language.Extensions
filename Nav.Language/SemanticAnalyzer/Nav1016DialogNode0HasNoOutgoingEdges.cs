using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

public class Nav1016DialogNode0HasNoOutgoingEdges: NavAnalyzer {

    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.DeadCode.Nav1016DialogNode0HasNoOutgoingEdges;

    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        // The dialog node '{0}' has no outgoing edges
        //==============================
        foreach (var dialogNode in taskDefinition.NodeDeclarations.OfType<IDialogNodeSymbol>()) {

            if (dialogNode.Incomings.Any() && !dialogNode.Outgoings.Any()) {

                yield return new Diagnostic(
                    dialogNode.Incomings.First().Location,
                    dialogNode.Incomings.Select(edge => edge.Location).Skip(1),
                    Descriptor,
                    dialogNode.Name);
            }
        }
    }

}