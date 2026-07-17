using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

/// <summary>
/// Nav1017 (<c>The view node '{0}' is not required by the code and can be safely removed</c>,
/// Dead-Code-Hinweis der Kategorie <see cref="DiagnosticCategory.DeadCode"/>): Ein
/// <c>view</c>-Knoten (<see cref="IViewNodeSymbol"/>), auf den keine Referenz aus dem
/// Transitionsblock existiert (<see cref="INodeSymbol.References"/>), ist überflüssig — die View
/// kommt im Workflow nicht vor, z.B. <c>view v;</c> ohne jede Kante von oder auf <c>v</c>.
/// Gemeldet wird über die gesamte Knoten-Deklaration. Deckt damit den Fall „ganz ohne Kanten" ab,
/// den <see cref="Nav0116ViewNode0HasNoIncomingEdges"/> und
/// <see cref="Nav0117ViewNode0HasNoOutgoingEdges"/> bewusst aussparen.
/// </summary>
public class Nav1017ViewNode0NotRequired: NavAnalyzer {

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.DeadCode.Nav1017ViewNode0NotRequired;

    /// <inheritdoc/>
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