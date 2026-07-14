using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

/// <summary>
/// Nav1002 (<c>The using directive for '{0}' appeared previously in this file and can be safely
/// removed</c>, Dead-Code-Hinweis der Kategorie <see cref="DiagnosticCategory.DeadCode"/>):
/// Wiederholte <c>[using …]</c>-Deklarationen (<see cref="CodeUsingDeclarationSyntax"/>) im
/// Datei-Kopf sind überflüssig. Die Deklarationen werden über den Text ihrer Namespace-Angabe
/// (<see cref="CodeUsingDeclarationSyntax.Namespace"/>) gruppiert; die jeweils erste Deklaration
/// eines Namespaces bleibt unbeanstandet, jede Wiederholung wird an ihrer eigenen Deklaration
/// gemeldet — bei <c>[using Foo] [using Foo] [using Foo]</c> also die zweite und dritte.
/// Deklarationen ohne Namespace-Angabe werden übergangen.
/// </summary>
public class Nav1002UsingDirective0AppearedPreviously: NavAnalyzer {

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.DeadCode.Nav1002UsingDirective0AppearedPreviously;

    /// <inheritdoc cref="INavAnalyzer.Analyze"/>
    /// <remarks>
    /// Geprüft wird der Datei-Kopf, nicht eine einzelne Task-Deklaration oder -Definition —
    /// deshalb überschreibt dieser Analyzer den Modell-Einstieg direkt, der Task-bezogene
    /// Auffächer der Basisklasse entfällt.
    /// </remarks>
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