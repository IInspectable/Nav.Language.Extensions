using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

/// <summary>
/// Nav0108 (<c>The end node has no incoming edges</c>, Warnung): Ein deklarierter
/// <c>end</c>-Knoten (<see cref="IEndNodeSymbol"/>) ohne eingehende Kanten
/// (<see cref="ITargetNodeSymbol.Incomings"/>) wird nie erreicht — <c>end;</c> ohne ein
/// <c>… --&gt; end;</c> im Transitionsblock. Gemeldet wird an der <c>end</c>-Deklaration.
/// Anders als <see cref="Nav0107ExitNode0HasNoIncomingEdges"/> prüft dieser Analyzer auch
/// Task-Definitionen ganz ohne Kanten und bietet keine Unterdrückung per Kommentar.
/// </summary>
public class Nav0108EndNodeHasNoIncomingEdges: NavAnalyzer {

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0108EndNodeHasNoIncomingEdges;

    /// <inheritdoc/>
    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        //  The end node has no incoming edges
        //==============================
        foreach (var endNode in taskDefinition.NodeDeclarations.OfType<IEndNodeSymbol>()) {
               
            if (!endNode.Incomings.Any()) {

                yield return new Diagnostic(
                    endNode.Location,
                    Descriptor,
                    endNode.Name);
            }
        }
    }

}