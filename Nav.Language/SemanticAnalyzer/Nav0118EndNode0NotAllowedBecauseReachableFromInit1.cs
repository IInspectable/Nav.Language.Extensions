using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer;

public class Nav0118EndNode0NotAllowedBecauseReachableFromInit1: NavAnalyzer {

    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0118EndNode0NotAllowedBecauseReachableFromInit1;

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
