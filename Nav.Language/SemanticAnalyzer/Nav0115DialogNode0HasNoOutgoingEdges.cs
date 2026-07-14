using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

/// <summary>
/// Nav0115 (<c>The dialog node '{0}' has no outgoing edges</c>, Warnung): Ein <c>dialog</c>-Knoten
/// (<see cref="IDialogNodeSymbol"/>), den Kanten erreichen
/// (<see cref="ITargetNodeSymbol.Incomings"/>), von dem aber keine Trigger-Transition ausgeht
/// (<see cref="IGuiNodeSymbol.Outgoings"/>), ist eine Sackgasse — aus dem angezeigten Dialog führt
/// kein Weg weiter, z.B. <c>I1 --&gt; C;</c> ohne eine Transition <b>von</b> <c>C</c> weg. Gemeldet
/// wird an der Knoten-Deklaration; ein Dialog-Knoten ganz ohne Kanten bleibt hier unbeanstandet.
/// Das Dead-Code-Gegenstück <see cref="Nav1016DialogNode0HasNoOutgoingEdges"/> meldet unter
/// derselben Bedingung an den eingehenden Kanten selbst.
/// </summary>
public class Nav0115DialogNode0HasNoOutgoingEdges: NavAnalyzer {

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0115DialogNode0HasNoOutgoingEdges;

    /// <inheritdoc/>
    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        // The dialog node '{0}' has no outgoing edges
        //==============================
        foreach (var dialogNode in taskDefinition.NodeDeclarations.OfType<IDialogNodeSymbol>()) {

            if (dialogNode.Incomings.Any() && !dialogNode.Outgoings.Any()) {

                yield return new Diagnostic(
                    dialogNode.Location,
                    Descriptor,
                    dialogNode.Name);
            }
        }
    }

}