using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

/// <summary>
/// Nav0116 (<c>The view node '{0}' has no incoming edges</c>, Warnung): Ein <c>view</c>-Knoten
/// (<see cref="IViewNodeSymbol"/>), von dem Trigger-Transitionen ausgehen
/// (<see cref="IGuiNodeSymbol.Outgoings"/>), den aber selbst keine Kante erreicht
/// (<see cref="ITargetNodeSymbol.Incomings"/>), ist unerreichbar — die View wird nie angezeigt,
/// z.B. <c>view v1; v1 --&gt; e1 on trigger;</c> ohne eine Kante <b>auf</b> <c>v1</c>. Gemeldet
/// wird an der Knoten-Deklaration; ein View-Knoten ganz ohne Kanten bleibt hier unbeanstandet. Das
/// Dead-Code-Gegenstück <see cref="Nav1018ViewNode0HasNoIncomingEdges"/> meldet unter derselben
/// Bedingung an den ausgehenden Trigger-Transitionen selbst.
/// </summary>
public class Nav0116ViewNode0HasNoIncomingEdges: NavAnalyzer {

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0116ViewNode0HasNoIncomingEdges;

    /// <inheritdoc/>
    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        // The view node '{0}' has no incoming edges
        //==============================
        foreach (var viewNode in taskDefinition.NodeDeclarations.OfType<IViewNodeSymbol>()) {

            if (viewNode.Outgoings.Any() && !viewNode.Incomings.Any()) {

                yield return (new Diagnostic(
                    viewNode.Location,
                    Descriptor,
                    viewNode.Name));
            }
        }
    }

}