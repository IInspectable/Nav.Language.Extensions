using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

/// <summary>
/// Nav0106 (<c>End node '{0}' can only be reached by a goto edge (--&gt;)</c>, Fehler): Der
/// <c>end</c>-Knoten (<see cref="IEndNodeSymbol"/>) darf nur über eine Goto-Kante
/// (<see cref="EdgeMode.Goto"/>, <c>--&gt;</c>) erreicht werden; modale (<c>o-&gt;</c>) und
/// nicht-modale (<c>==&gt;</c>) Kanten auf das Ende sind Fehler, z.B.
/// <c>V o-&gt; end on Trigger;</c>. Die Diagnose sitzt am Kanten-Operator
/// (<see cref="IEdgeModeSymbol"/>); Kanten ohne Operator werden übergangen.
/// </summary>
public class Nav0106EndNode0MustOnlyReachedByGoTo: NavAnalyzer {

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0106EndNode0MustOnlyReachedByGoTo;

    /// <inheritdoc/>
    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        // End node '{0}' can only be reached by a goto edge (-->)
        //==============================
        foreach (var transition in taskDefinition.Edges()) {

            if (transition.TargetReference?.Declaration is EndNodeSymbol &&
                transition.EdgeMode          != null                     &&
                transition.EdgeMode.EdgeMode != EdgeMode.Goto) {

                yield return (new Diagnostic(
                    transition.EdgeMode.Location,
                    Descriptor,
                    transition.TargetReference.Name));
            }
        }
    }

}