using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

public class Nav0107ExitNode0HasNoIncomingEdges: NavAnalyzer {

    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0107ExitNode0HasNoIncomingEdges;

    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        //  The exit node '{0}' has no incoming edges
        //==============================
        // Wenn überhaupt keine Edges definiert sind, werten wir bisweilen unbenutzte Verbindungspunkte nicht als Warnung
        if (!taskDefinition.Edges().Any()) {
            yield break;
        }
        foreach (var exitNode in taskDefinition.NodeDeclarations.OfType<IExitNodeSymbol>()) {

            if (context.IsWarningDisabled(exitNode, Descriptor)) {
                continue;
            }

            if (!exitNode.Incomings.Any()) {

                yield return new Diagnostic(
                    exitNode.Location,
                    Descriptor,
                    exitNode.Name);
            }
        }

    }

}