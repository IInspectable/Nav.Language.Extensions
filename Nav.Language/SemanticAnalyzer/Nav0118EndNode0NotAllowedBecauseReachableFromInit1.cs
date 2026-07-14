using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer;

/// <summary>
/// Nav0118 (<c>End node '{0}' not allowed here because it's reachable from init node '{1}'</c>,
/// Fehler): Der <c>end</c>-Knoten (<see cref="IEndNodeSymbol"/>) darf nicht auf dem Einstiegspfad
/// eines Tasks liegen: Ein aus einer Init-Transition (<see cref="IInitNodeSymbol.Outgoings"/>) —
/// transitiv über Choice-Knoten hinweg via
/// <see cref="EdgeExtensions.GetReachableCalls(IEdge)"/> — erreichbarer <c>end</c>-Aufruf liefert
/// das Framework-Kommando <c>END</c>, das anders als <c>GOTO_GUI</c>/<c>GOTO_TASK</c>/
/// <c>TASK_RESULT</c>/<c>CANCEL</c> nicht <c>IINIT_TASK</c> ist — die generierte
/// <c>Begin</c>-Methode wäre nicht übersetzbar (CS0266). Das Kanten-Gegenstück
/// <see cref="Nav0110Edge0NotAllowedIn1BecauseItsReachableFromInit2"/> prüft nur Nicht-Goto-Kanten;
/// <c>end</c> wird aber ausschließlich per Goto erreicht
/// (<see cref="Nav0106EndNode0MustOnlyReachedByGoTo"/>) und fiele damit durch — z.B. meldet
/// <c>I1 --&gt; C; C --&gt; end;</c> ein <c>End node 'end' not allowed here because it's reachable
/// from init node 'I1'</c>. Die Diagnose sitzt am Operator der Goto-Kante auf <c>end</c>
/// (<see cref="Call.EdgeMode"/>).
/// </summary>
public class Nav0118EndNode0NotAllowedBecauseReachableFromInit1: NavAnalyzer {

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0118EndNode0NotAllowedBecauseReachableFromInit1;

    /// <inheritdoc/>
    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        // End node '{0}' not allowed here because it's reachable from init node '{1}'
        //==============================
        // Ein aus einem Init erreichbarer End-Knoten liefert das Framework-Kommando END, das — anders
        // als GOTO_GUI/GOTO_TASK/TASK_RESULT/CANCEL — NICHT IINIT_TASK ist. Der generierte
        // `IINIT_TASK Begin() { … return EndNonModal(); }` wäre also nicht übersetzbar (CS0266). Nav0110
        // deckt nur die Nicht-Goto-Kanten ab; End wird ausschließlich per Goto erreicht (Nav0106) und
        // fällt damit durch Nav0110 hindurch — deshalb diese eigene Regel.
        foreach (var initNode in taskDefinition.NodeDeclarations.OfType<IInitNodeSymbol>()) {

            foreach (var initTransition in initNode.Outgoings) {
                foreach (var reachableCall in initTransition.GetReachableCalls()
                                                            .Where(c => c.Node              is IEndNodeSymbol &&
                                                                        c.EdgeMode.EdgeMode == EdgeMode.Goto)) {
                    yield return new Diagnostic(
                        reachableCall.EdgeMode.Location,
                        Descriptor,
                        reachableCall.Node.Name,
                        initNode.Name);
                }
            }
        }
    }

}
