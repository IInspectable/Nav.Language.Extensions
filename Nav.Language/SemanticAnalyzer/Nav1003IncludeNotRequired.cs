using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

/// <summary>
/// Nav1003 (<c>Taskref directive is not required by the code and can be safely removed</c>,
/// Dead-Code-Hinweis der Kategorie <see cref="DiagnosticCategory.DeadCode"/>): Eine
/// Include-Direktive <c>taskref "datei.nav";</c> (<see cref="IIncludeSymbol"/>, publiziert über
/// <see cref="CodeGenerationUnit.Includes"/>) ist überflüssig, wenn keine der aus der
/// eingebundenen Datei extrahierten Task-Deklarationen
/// (<see cref="IIncludeSymbol.TaskDeclarations"/>) von einem Task-Knoten referenziert wird
/// (<see cref="ITaskDeclarationSymbol.References"/>). Gemeldet wird an der Direktive selbst
/// (<see cref="IIncludeSymbol.Syntax"/>).
/// </summary>
public class Nav1003IncludeNotRequired: NavAnalyzer {

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.DeadCode.Nav1003IncludeNotRequired;

    /// <inheritdoc cref="INavAnalyzer.Analyze"/>
    /// <remarks>
    /// Geprüft werden die Include-Direktiven der Datei (<see cref="CodeGenerationUnit.Includes"/>) —
    /// deshalb überschreibt dieser Analyzer den Modell-Einstieg direkt, der Task-bezogene
    /// Auffächer der Basisklasse entfällt.
    /// </remarks>
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