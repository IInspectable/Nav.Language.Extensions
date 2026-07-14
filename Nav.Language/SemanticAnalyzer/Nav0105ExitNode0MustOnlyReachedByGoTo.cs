using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

/// <summary>
/// Nav0105 (<c>Exit node '{0}' can only be reached by a goto edge (--&gt;)</c>, Fehler): Ein
/// <c>exit</c>-Knoten (<see cref="IExitNodeSymbol"/>) darf nur über eine Goto-Kante
/// (<see cref="EdgeMode.Goto"/>, <c>--&gt;</c>) erreicht werden; modale (<c>o-&gt;</c>) und
/// nicht-modale (<c>==&gt;</c>) Kanten auf einen Exit sind Fehler, z.B.
/// <c>V ==&gt; e1 on Trigger;</c>. Die Diagnose sitzt am Kanten-Operator
/// (<see cref="IEdgeModeSymbol"/>); Kanten ohne Operator werden übergangen.
/// </summary>
public class Nav0105ExitNode0MustOnlyReachedByGoTo: NavAnalyzer {

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0105ExitNode0MustOnlyReachedByGoTo;

    /// <inheritdoc/>
    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        // Exit node '{0}' can only be reached by a goto edge (-->)
        //==============================
        foreach (var transition in taskDefinition.Edges()) {

            if (transition.TargetReference?.Declaration is ExitNodeSymbol &&
                transition.EdgeMode          != null                      &&
                transition.EdgeMode.EdgeMode != EdgeMode.Goto) {

                yield return (new Diagnostic(
                    transition.EdgeMode.Location,
                    Descriptor,
                    transition.TargetReference.Name));
            }
        }
    }

}