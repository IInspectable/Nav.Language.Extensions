#nullable enable

using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

public class Nav1003IncludeNotRequired: NavAnalyzer {

    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.DeadCode.Nav1003IncludeNotRequired;

    public override IEnumerable<Diagnostic> Analyze(CodeGenerationUnit codeGenerationUnit, AnalyzerContext context) {

        //==============================
        // Taskref directive is not required by the code and can be safely removed
        //==============================
        var unusedIncludes = codeGenerationUnit.Includes.Where(i => !i.TaskDeclarations.SelectMany(td => td.References).Any());
        foreach (var includeSymbol in unusedIncludes) {

            yield return new Diagnostic(
                includeSymbol.Syntax.GetLocation(),
                Descriptor);
        }
    }

}