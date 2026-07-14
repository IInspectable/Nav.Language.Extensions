using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

/// <summary>
/// Nav0114 (<c>The dialog node '{0}' has no incoming edges</c>, Warnung): Ein <c>dialog</c>-Knoten
/// (<see cref="IDialogNodeSymbol"/>), von dem Trigger-Transitionen ausgehen
/// (<see cref="IGuiNodeSymbol.Outgoings"/>), den aber selbst keine Kante erreicht
/// (<see cref="ITargetNodeSymbol.Incomings"/>), ist unerreichbar — der Dialog wird nie angezeigt,
/// z.B. <c>dialog C; C --&gt; e1 on t1;</c> ohne eine Kante <b>auf</b> <c>C</c>. Gemeldet wird an
/// der Knoten-Deklaration; ein Dialog-Knoten ganz ohne Kanten bleibt hier unbeanstandet. Das
/// Dead-Code-Gegenstück <see cref="Nav1015DialogNode0HasNoIncomingEdges"/> meldet unter derselben
/// Bedingung an den ausgehenden Trigger-Transitionen selbst.
/// </summary>
public class Nav0114DialogNode0HasNoIncomingEdges: NavAnalyzer {

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0114DialogNode0HasNoIncomingEdges;

    /// <inheritdoc/>
    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        // The dialog node '{0}' has no incoming edges
        //==============================
        foreach (var dialogNode in taskDefinition.NodeDeclarations.OfType<IDialogNodeSymbol>()) {

            if (dialogNode.Outgoings.Any() && !dialogNode.Incomings.Any()) {

                yield return (new Diagnostic(
                    dialogNode.Location,
                    Descriptor,
                    dialogNode.Name));
            }
        }
    }

}