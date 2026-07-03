using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

public class Nav0110Edge0NotAllowedIn1BecauseItsReachableFromInit2: NavAnalyzer {

    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0110Edge0NotAllowedIn1BecauseItsReachableFromInit2;

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