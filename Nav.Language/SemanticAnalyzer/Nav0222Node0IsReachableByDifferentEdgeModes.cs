using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

/// <summary>
/// Nav0222 (<c>Node '{0}' is reached by edges of different modes</c>, Fehler): Ein Knoten muss von
/// einer Kante aus in einem einheitlichen Anzeige-Modus erreicht werden. Für jede Kante des
/// Transitionsblocks (<see cref="ITaskDefinitionSymbol.Edges"/>) werden die erreichbaren Aufrufe
/// (<see cref="EdgeExtensions.GetReachableCalls(IEdge)"/>, transitiv über Choice-Knoten aufgelöst)
/// nach Zielknoten gruppiert; treffen auf denselben Knoten Aufrufe mit verschiedenen Kantenmodi
/// (<see cref="EdgeMode"/>: <c>--&gt;</c>/<c>o-&gt;</c>/<c>==&gt;</c>), wird das gemeldet — z.B.
/// erreichen hinter <c>v1 --&gt; C;</c> die Choice-Ausgänge <c>C --&gt; v1; C o-&gt; v1;</c> den
/// Knoten <c>v1</c> per Goto <b>und</b> modal (<c>Node 'v1' is reached by edges of different
/// modes</c>). Die Diagnose sitzt am Kanten-Operator des ersten widersprüchlichen Aufrufs
/// (<see cref="Call.EdgeMode"/>), die übrigen Operatoren sind Zusatz-Fundstellen.
/// </summary>
public class Nav0222Node0IsReachableByDifferentEdgeModes: NavAnalyzer {

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0222Node0IsReachableByDifferentEdgeModes;

    /// <inheritdoc/>
    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        // Node '{0}' is reached by edges of different modes
        //==============================
        foreach (IEdge edge in taskDefinition.Edges()) {

            foreach (var nodeCalls in edge.GetReachableCalls().GroupBy(c => c.Node)) {

                if (nodeCalls.GroupBy(c => c.EdgeMode.EdgeMode).Count() > 1) {

                    yield return new Diagnostic(
                        nodeCalls.First().EdgeMode.Location,
                        nodeCalls.Skip(1).Select(call => call.EdgeMode.Location),
                        Descriptor,
                        nodeCalls.Key.Name
                    );
                }
            }

        }
    }

}