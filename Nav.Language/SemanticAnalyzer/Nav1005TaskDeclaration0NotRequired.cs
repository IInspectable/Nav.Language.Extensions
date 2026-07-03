using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

public class Nav1005TaskDeclaration0NotRequired: NavAnalyzer {

    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.DeadCode.Nav1005TaskDeclaration0NotRequired;
        
    public override IEnumerable<Diagnostic> Analyze(ITaskDeclarationSymbol taskDeclaration, AnalyzerContext context) {
        //==============================
        // Taskref '{0}' is not required by the code and can be safely removed
        //==============================
        if (!taskDeclaration.IsIncluded                                     && 
            taskDeclaration.Origin == TaskDeclarationOrigin.TaskDeclaration && 
            !taskDeclaration.References.Any()) {

            var location = taskDeclaration.Syntax?.GetLocation() ?? taskDeclaration.Location;

            yield return new Diagnostic(
                location,
                Descriptor,
                taskDeclaration.Name);
        }

    }


}