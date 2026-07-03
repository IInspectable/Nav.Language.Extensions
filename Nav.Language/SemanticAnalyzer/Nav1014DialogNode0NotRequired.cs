using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

public class Nav1014DialogNode0NotRequired: NavAnalyzer {

    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.DeadCode.Nav1014DialogNode0NotRequired;

    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        //  The dialog node '{0}' is not required by the code and can be safely removed
        //==============================
        foreach (var dialogNode in taskDefinition.NodeDeclarations.OfType<IDialogNodeSymbol>()) {

            if (!dialogNode.References.Any()) {

                yield return new Diagnostic(
                    dialogNode.Syntax.GetLocation(),
                    Descriptor,
                    dialogNode.Name);
            }
        }
    }

}