using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

/// <summary>
/// Nav1015 (<c>The dialog node '{0}' has no incoming edges</c>, Dead-Code-Hinweis der Kategorie
/// <see cref="DiagnosticCategory.DeadCode"/>): Dead-Code-Gegenstück zu
/// <see cref="Nav0114DialogNode0HasNoIncomingEdges"/> — dieselbe Bedingung (ein
/// <c>dialog</c>-Knoten (<see cref="IDialogNodeSymbol"/>) mit ausgehenden Trigger-Transitionen
/// (<see cref="IGuiNodeSymbol.Outgoings"/>), den aber selbst keine Kante erreicht
/// (<see cref="ITargetNodeSymbol.Incomings"/>)), gemeldet aber nicht an der Knoten-Deklaration,
/// sondern an den ausgehenden Trigger-Transitionen selbst (die erste als Hauptfundstelle, alle
/// weiteren als Zusatz-Fundstellen) — so kann der Editor die nie durchlaufenen Transitionen
/// abgedunkelt darstellen.
/// </summary>
public class Nav1015DialogNode0HasNoIncomingEdges: NavAnalyzer {

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.DeadCode.Nav1015DialogNode0HasNoIncomingEdges;

    /// <inheritdoc/>
    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        // The dialog node '{0}' has no incoming edges
        //==============================
        foreach (var dialogNode in taskDefinition.NodeDeclarations.OfType<IDialogNodeSymbol>()) {

            if (dialogNode.Outgoings.Any() && !dialogNode.Incomings.Any()) {

                yield return new Diagnostic(
                    dialogNode.Outgoings.First().Location,
                    dialogNode.Outgoings.Select(edge => edge.Location).Skip(1),
                    Descriptor,
                    dialogNode.Name);
            }
        }
    }

}