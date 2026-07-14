using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

/// <summary>
/// Nav0112 (<c>The choice node '{0}' has no outgoing edges</c>, Warnung): Ein <c>choice</c>-Knoten
/// (<see cref="IChoiceNodeSymbol"/>), den Kanten erreichen
/// (<see cref="ITargetNodeSymbol.Incomings"/>), der aber keine ausgehende Kante besitzt
/// (<see cref="IChoiceNodeSymbol.Outgoings"/>), ist eine Sackgasse — die Verzweigung hat keinen
/// Folgeweg, z.B. <c>I1 --&gt; Choice_e1;</c> ohne eine Kante <b>von</b> <c>Choice_e1</c> weg.
/// Gemeldet wird an der Knoten-Deklaration; ein Choice-Knoten ganz ohne Kanten bleibt hier
/// unbeanstandet. Das Dead-Code-Gegenstück <see cref="Nav1008ChoiceNode0HasNoOutgoingEdges"/>
/// meldet unter derselben Bedingung an den eingehenden Kanten selbst.
/// </summary>
public class Nav0112ChoiceNode0HasNoOutgoingEdges: NavAnalyzer {

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0112ChoiceNode0HasNoOutgoingEdges;

    /// <inheritdoc/>
    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        //  The choice node '{0}' has no outgoing edges
        //==============================
        foreach (var choiceNode in taskDefinition.NodeDeclarations.OfType<IChoiceNodeSymbol>()) {

            if (choiceNode.Incomings.Any() && !choiceNode.Outgoings.Any()) {

                yield return new Diagnostic(
                    choiceNode.Location,
                    Descriptor,
                    choiceNode.Name);
            }
        }

    }

}