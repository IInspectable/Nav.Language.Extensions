using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

public class Nav0114DialogNode0HasNoIncomingEdges: NavAnalyzer {

    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0114DialogNode0HasNoIncomingEdges;

    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        // The dialog node '{0}' has no incoming edges
        //==============================
        foreach (var dialogNode in taskDefinition.NodeDeclarations.OfType<IDialogNodeSymbol>()) {

            if (dialogNode.Outgoings.Any() && !dialogNode.Incomings.Any()) {

                yield return (new Diagnostic(
                    dialogNode.Location,
                    Descriptor,
                    dialogNode.Name));
            }
        }
    }

}