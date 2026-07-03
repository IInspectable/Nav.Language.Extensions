using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

public class Nav1017ViewNode0NotRequired: NavAnalyzer {

    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.DeadCode.Nav1017ViewNode0NotRequired;

    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        // The view node '{0}' is not required by the code and can be safely removed
        //==============================
        foreach (var viewNode in taskDefinition.NodeDeclarations.OfType<IViewNodeSymbol>()) {

            if (!viewNode.References.Any()) {

                yield return (new Diagnostic(
                    viewNode.Syntax.GetLocation(),
                    Descriptor,
                    viewNode.Name));

            }
        }
    }

}