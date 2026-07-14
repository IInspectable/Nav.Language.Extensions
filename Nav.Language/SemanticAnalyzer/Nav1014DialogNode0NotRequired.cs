using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

/// <summary>
/// Nav1014 (<c>The dialog node '{0}' is not required by the code and can be safely removed</c>,
/// Dead-Code-Hinweis der Kategorie <see cref="DiagnosticCategory.DeadCode"/>): Ein
/// <c>dialog</c>-Knoten (<see cref="IDialogNodeSymbol"/>), auf den kein Verweis aus dem
/// Transitionsblock existiert (<see cref="INodeSymbol.References"/>), ist überflüssig — der
/// Dialog kommt im Workflow nicht vor, z.B. <c>dialog d;</c> ohne jede Kante von oder auf
/// <c>d</c>. Gemeldet wird über die gesamte Knoten-Deklaration. Deckt damit den Fall „ganz ohne
/// Kanten" ab, den <see cref="Nav0114DialogNode0HasNoIncomingEdges"/> und
/// <see cref="Nav0115DialogNode0HasNoOutgoingEdges"/> bewusst aussparen.
/// </summary>
public class Nav1014DialogNode0NotRequired: NavAnalyzer {

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.DeadCode.Nav1014DialogNode0NotRequired;

    /// <inheritdoc/>
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