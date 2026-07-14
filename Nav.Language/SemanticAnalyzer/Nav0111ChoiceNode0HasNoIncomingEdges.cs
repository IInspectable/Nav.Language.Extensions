using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

/// <summary>
/// Nav0111 (<c>The choice node '{0}' has no incoming edges</c>, Warnung): Ein <c>choice</c>-Knoten
/// (<see cref="IChoiceNodeSymbol"/>), von dem Kanten ausgehen
/// (<see cref="IChoiceNodeSymbol.Outgoings"/>), den aber selbst keine Kante erreicht
/// (<see cref="ITargetNodeSymbol.Incomings"/>), ist unerreichbar — seine Verzweigung kann nie
/// durchlaufen werden, z.B. <c>Choice_e1 o-&gt; v1;</c> ohne eine Kante <b>auf</b> <c>Choice_e1</c>.
/// Gemeldet wird an der Knoten-Deklaration; ein Choice-Knoten ganz ohne Kanten bleibt hier
/// unbeanstandet. Das Dead-Code-Gegenstück <see cref="Nav1007ChoiceNode0HasNoIncomingEdges"/>
/// meldet unter derselben Bedingung an den ausgehenden Kanten selbst.
/// </summary>
public class Nav0111ChoiceNode0HasNoIncomingEdges: NavAnalyzer {

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0111ChoiceNode0HasNoIncomingEdges;

    /// <inheritdoc/>
    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        //  The choice node '{0}' has no incoming edges
        //==============================
        foreach (var choiceNode in taskDefinition.NodeDeclarations.OfType<IChoiceNodeSymbol>()) {

            if (choiceNode.Outgoings.Any() && !choiceNode.Incomings.Any()) {

                yield return new Diagnostic(
                    choiceNode.Location,
                    Descriptor,
                    choiceNode.Name);
            }
        }

    }

}