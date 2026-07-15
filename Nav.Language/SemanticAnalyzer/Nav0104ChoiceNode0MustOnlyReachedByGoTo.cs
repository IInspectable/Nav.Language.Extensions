using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

/// <summary>
/// Nav0104 (<c>Choice node '{0}' can only be reached by a goto edge (--&gt;)</c>, Fehler): Ein
/// <c>choice</c>-Knoten (<see cref="IChoiceNodeSymbol"/>) darf nur über eine Goto-Kante
/// (<see cref="EdgeMode.Goto"/>, <c>--&gt;</c>) erreicht werden — die Choice trifft nur die
/// Verzweigungs-Entscheidung, den Kantenmodus des tatsächlichen Ziels bestimmen erst die Kanten
/// hinter ihr (siehe <see cref="EdgeExtensions.GetReachableCalls(IEdge)"/>). Modale
/// (<c>o-&gt;</c>) und nicht-modale (<c>==&gt;</c>) Kanten auf eine Choice sind Fehler, z.B.
/// <c>I1 o-&gt; Choice_e1;</c>. Die Diagnose sitzt am Kanten-Operator
/// (<see cref="IEdgeModeSymbol"/>); Kanten ohne Operator werden übergangen.
/// </summary>
public class Nav0104ChoiceNode0MustOnlyReachedByGoTo: NavAnalyzer {

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0104ChoiceNode0MustOnlyReachedByGoTo;

    /// <inheritdoc/>
    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        // Choice node '{0}' can only be reached by a goto edge (-->)
        //==============================
        foreach (var transition in taskDefinition.Edges()) {

            if (transition.TargetReference?.Declaration is ChoiceNodeSymbol &&
                transition.EdgeMode          != null                        &&
                transition.EdgeMode.EdgeMode != EdgeMode.Goto) {

                yield return (new Diagnostic(
                    transition.EdgeMode.Location,
                    Descriptor,
                    transition.TargetReference.Name));
            }
        }
    }

}