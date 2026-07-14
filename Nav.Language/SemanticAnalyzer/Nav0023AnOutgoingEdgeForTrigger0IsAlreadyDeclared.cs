using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

/// <summary>
/// Nav0023 (<c>An outgoing edge for trigger '{0}' is already declared</c>, Fehler): An einem
/// GUI-Knoten darf jeder Trigger (<see cref="ITriggerSymbol"/>) nur eine ausgehende Kante
/// auslösen — welche Kante der Trigger nimmt, wäre sonst nicht eindeutig:
/// <code>
/// A --&gt; e1 on Foo;
/// A --&gt; e1 on Foo;   // Nav0023
/// </code>
/// Geprüft über alle <see cref="ITaskDefinitionSymbol.TriggerTransitions"/>, je aufgelöstem
/// Quellknoten; Kanten mit unauflösbarem Quellknoten werden übergangen (dort meldet bereits
/// Nav0011). Die Diagnose zeigt auf die wiederholte Verwendung und führt die frühere als
/// zusätzliche Fundstelle.
/// </summary>
public class Nav0023AnOutgoingEdgeForTrigger0IsAlreadyDeclared: NavAnalyzer {

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0023AnOutgoingEdgeForTrigger0IsAlreadyDeclared;

    /// <inheritdoc/>
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