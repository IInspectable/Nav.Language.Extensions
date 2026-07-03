using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

public class Nav1002UsingDirective0AppearedPreviously: NavAnalyzer {

    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.DeadCode.Nav1002UsingDirective0AppearedPreviously;

    public override IEnumerable<Diagnostic> Analyze(CodeGenerationUnit codeGenerationUnit, AnalyzerContext context) {
        //==============================
        // The using directive for '{0}' appeared previously in this file.\r\nUsing Directive is unnecessary
        //==============================
        var candidates = codeGenerationUnit.Syntax.DescendantNodes<CodeUsingDeclarationSyntax>()
                                           .Where(usingSyntax => usingSyntax.Namespace != null)
                                           // Namespace ist durch das vorherige Where non-null; der Compiler verengt nicht über die Lambda-Grenze.
                                           .Select(usingSyntax => (Namespace: usingSyntax.Namespace!.ToString(), Syntax: usingSyntax))
                                           .GroupBy(p => p.Namespace);

        foreach (var candidate in candidates.Where(c => c.Count() > 1)) {
            // Die erste Using Deklaration ist noch OK
            foreach (var duplicate in candidate.Skip(1)) {
                yield return new Diagnostic(
                    duplicate.Syntax.GetLocation(),
                    Descriptor,
                    candidate.Key);
            }

        }

    }

}