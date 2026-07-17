using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

/// <summary>
/// Nav1018 (<c>The view node '{0}' has no incoming edges</c>, Dead-Code-Hinweis der Kategorie
/// <see cref="DiagnosticCategory.DeadCode"/>): Dead-Code-Gegenstück zu
/// <see cref="Nav0116ViewNode0HasNoIncomingEdges"/> — dieselbe Bedingung (ein <c>view</c>-Knoten
/// (<see cref="IViewNodeSymbol"/>) mit ausgehenden Trigger-Transitionen
/// (<see cref="IGuiNodeSymbol.Outgoings"/>), den aber selbst keine Kante erreicht
/// (<see cref="ITargetNodeSymbol.Incomings"/>)), gemeldet aber nicht an der Knoten-Deklaration,
/// sondern an den ausgehenden Trigger-Transitionen selbst (die erste als Hauptfundstelle, alle
/// weiteren als Zusatz-Fundstellen) — so kann der Editor die nie durchlaufenen Transitionen
/// abgedunkelt darstellen.
/// </summary>
public class Nav1018ViewNode0HasNoIncomingEdges: NavAnalyzer {

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.DeadCode.Nav1018ViewNode0HasNoIncomingEdges;

    /// <inheritdoc/>
    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        // The view node '{0}' has no incoming edges
        //==============================
        foreach (var viewNode in taskDefinition.NodeDeclarations.OfType<IViewNodeSymbol>()) {

            if (viewNode.Outgoings.Any() && !viewNode.Incomings.Any()) {

                yield return (new Diagnostic(
                    viewNode.Outgoings.First().Location,
                    viewNode.Outgoings.Select(edge => edge.Location).Skip(1),
                    Descriptor,
                    viewNode.Name));
            }
        }
    }

}