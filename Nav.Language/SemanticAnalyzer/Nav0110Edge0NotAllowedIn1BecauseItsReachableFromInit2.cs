using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

/// <summary>
/// Nav0110 (<c>'{0}' edge not allowed here because '{1}' is reachable from init node '{2}'</c>,
/// Fehler): Auf dem Einstiegspfad eines Tasks sind nur Goto-Kanten (<see cref="EdgeMode.Goto"/>,
/// <c>--&gt;</c>) zulässig: Jeder Aufruf, der von einer Init-Transition
/// (<see cref="IInitNodeSymbol.Outgoings"/>) aus erreichbar ist — transitiv über Choice-Knoten
/// hinweg aufgelöst via <see cref="EdgeExtensions.GetReachableCalls(IEdge)"/> —, darf weder
/// modal (<c>o-&gt;</c>) noch nicht-modal (<c>==&gt;</c>) erfolgen. Das fängt insbesondere die
/// Kanten <b>hinter</b> einer vom Init erreichten Choice, die
/// <see cref="Nav0104ChoiceNode0MustOnlyReachedByGoTo"/> nicht prüft: z.B. meldet
/// <c>I1 --&gt; Choice_e1; Choice_e1 o-&gt; v1;</c> ein <c>'Modal Edge' edge not allowed here
/// because 'v1' is reachable from init node 'I1'</c>. Die Diagnose sitzt am Operator der
/// verbotenen Kante (<see cref="Call.EdgeMode"/>); das Gegenstück für <c>end</c>-Ziele ist
/// <see cref="Nav0118EndNode0NotAllowedBecauseReachableFromInit1"/>.
/// </summary>
public class Nav0110Edge0NotAllowedIn1BecauseItsReachableFromInit2: NavAnalyzer {

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0110Edge0NotAllowedIn1BecauseItsReachableFromInit2;

    /// <inheritdoc/>
    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        // '{0}' edge not allowed here because '{1}' is reachable from init node '{2}'
        //==============================
        foreach (var initNode in taskDefinition.NodeDeclarations.OfType<IInitNodeSymbol>()) {

            // Interessanterweise darf eine Init-Transition merhr als einen Ausgang haben, und hat somit
            // eine "eingebaute choice".
            foreach (var initTransition in initNode.Outgoings) {
                foreach (var reachableCall in initTransition.GetReachableCalls()
                                                            .Where(c => c.EdgeMode.EdgeMode != EdgeMode.Goto)) {
                    yield return new Diagnostic(
                        reachableCall.EdgeMode.Location,
                        DiagnosticDescriptors.Semantic.Nav0110Edge0NotAllowedIn1BecauseItsReachableFromInit2,
                        reachableCall.EdgeMode.DisplayName,
                        reachableCall.Node.Name,
                        initNode.Name);
                }
            }
        }
    }

}