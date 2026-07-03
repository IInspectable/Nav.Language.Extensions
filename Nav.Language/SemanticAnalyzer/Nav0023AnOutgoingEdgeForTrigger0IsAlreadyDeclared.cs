#nullable enable

using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

public class Nav0023AnOutgoingEdgeForTrigger0IsAlreadyDeclared: NavAnalyzer {

    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0023AnOutgoingEdgeForTrigger0IsAlreadyDeclared;

    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        // An outgoing edge for trigger '{0}' is already declared
        //==============================
        var triggerMap = new Dictionary<INodeSymbol, ITriggerSymbol>();
        foreach (var trans in taskDefinition.TriggerTransitions) {
            // Nicht deklarierte Sourcenodes interessieren uns nicht
            var nodeSymbol = trans.SourceReference?.Declaration;
            if (nodeSymbol == null) {
                continue;
            }

            triggerMap.TryGetValue(nodeSymbol, out var existing);

            foreach (var trigger in trans.Triggers) {

                if (existing != null && trigger.Name == existing.Name) {

                    yield return (new Diagnostic(
                        trigger.Location,
                        existing.Location,
                        Descriptor,
                        existing.Name));

                } else {
                    triggerMap[nodeSymbol] = trigger;
                }
            }
        }
    }

}