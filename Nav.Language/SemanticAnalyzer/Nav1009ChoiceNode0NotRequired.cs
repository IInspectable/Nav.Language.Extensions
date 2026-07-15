using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

/// <summary>
/// Nav1009 (<c>The choice node '{0}' is not required by the code and can be safely removed</c>,
/// Dead-Code-Hinweis der Kategorie <see cref="DiagnosticCategory.DeadCode"/>): Ein
/// <c>choice</c>-Knoten (<see cref="IChoiceNodeSymbol"/>), auf den keine Referenz aus dem
/// Transitionsblock existiert (<see cref="INodeSymbol.References"/>), ist überflüssig — z.B.
/// <c>choice C;</c> ohne jede Kante von oder auf <c>C</c>. Gemeldet wird über die gesamte
/// Knoten-Deklaration. Deckt damit den Fall „ganz ohne Kanten" ab, den
/// <see cref="Nav0111ChoiceNode0HasNoIncomingEdges"/> und
/// <see cref="Nav0112ChoiceNode0HasNoOutgoingEdges"/> bewusst aussparen.
/// </summary>
public class Nav1009ChoiceNode0NotRequired: NavAnalyzer {

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.DeadCode.Nav1009ChoiceNode0NotRequired;

    /// <inheritdoc/>
    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        //  The choice node '{0}' is not required by the code and can be safely removed
        //==============================
        foreach (var choiceNode in taskDefinition.NodeDeclarations.OfType<IChoiceNodeSymbol>()) {

            if (!choiceNode.References.Any()) {

                yield return new Diagnostic(
                    choiceNode.Syntax.GetLocation(),
                    Descriptor,
                    choiceNode.Name);
            }

        }

    }

}