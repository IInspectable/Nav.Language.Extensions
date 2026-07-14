using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

/// <summary>
/// Nav1005 (<c>Taskref '{0}' is not required by the code and can be safely removed</c>,
/// Dead-Code-Hinweis der Kategorie <see cref="DiagnosticCategory.DeadCode"/>): Eine explizite
/// <c>taskref Name { … }</c>-Deklaration (<see cref="TaskDeclarationOrigin.TaskDeclaration"/>)
/// der eigenen Datei (<see cref="ITaskDeclarationSymbol.IsIncluded"/> ist <c>false</c>) ist
/// überflüssig, wenn kein Task-Knoten sie referenziert
/// (<see cref="ITaskDeclarationSymbol.References"/>). Gemeldet wird über die gesamte
/// <c>taskref</c>-Deklaration. Implizite Deklarationen aus <c>task</c>-Definitionen und
/// inkludierte Deklarationen bleiben hier unbeanstandet (unbenutzte Includes meldet
/// <see cref="Nav1003IncludeNotRequired"/> an der Direktive).
/// </summary>
public class Nav1005TaskDeclaration0NotRequired: NavAnalyzer {

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.DeadCode.Nav1005TaskDeclaration0NotRequired;
        
    /// <inheritdoc/>
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