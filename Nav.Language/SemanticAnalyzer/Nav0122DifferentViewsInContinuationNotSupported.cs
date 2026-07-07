using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer;

public class Nav0122DifferentViewsInContinuationNotSupported: NavAnalyzer {

    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0122DifferentViewsInContinuationNotSupported;

    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        // Different views are not supported in a continuation
        //==============================
        // Aus einer Quelle heraus wird EINE mode-freie Show{View}-Methode generiert; sie kann die
        // Continuation nur auf GENAU EINEN tragenden GUI-Knoten legen. Erreichen die Continuations einer
        // Quelle (auch über Choices hinweg) verschiedene Views, ist das nicht abbildbar. Die „Quelle" ist
        // je nach Übergangsart unterschiedlich gepoolt:

        // Init-Transitionen: alle Ausgänge eines Init-Knotens gemeinsam.
        foreach (var initNode in taskDefinition.NodeDeclarations.OfType<IInitNodeSymbol>()) {
            foreach (var diagnostic in Analyze(initNode.Outgoings)) {
                yield return diagnostic;
            }
        }

        // Trigger-Transitionen: jede für sich.
        foreach (var triggerTransition in taskDefinition.TriggerTransitions) {
            foreach (var diagnostic in Analyze(new IEdge[] {triggerTransition})) {
                yield return diagnostic;
            }
        }

        // Exit-Transitionen: alle Ausgänge eines Task-Knotens gemeinsam.
        foreach (var taskNode in taskDefinition.NodeDeclarations.OfType<ITaskNodeSymbol>()) {
            foreach (var diagnostic in Analyze(taskNode.Outgoings)) {
                yield return diagnostic;
            }
        }
    }

    static IEnumerable<Diagnostic> Analyze(IEnumerable<IEdge> edges) {

        // In einer Continuation ist die Quelle immer ein GUI-Knoten (Nav0120); hier zählen die
        // verschiedenen tragenden GUI-Knoten.
        var guiNodeReferences = edges.SelectMany(edge => edge.GetReachableContinuations())
                                     .Select(continuation => continuation.SourceReference)
                                     .WhereNotNull()
                                     .DistinctBy(reference => reference.Declaration)
                                     .ToList();

        if (guiNodeReferences.Count > 1) {
            yield return new Diagnostic(
                guiNodeReferences[0].Location,
                guiNodeReferences.Skip(1).Select(reference => reference.Location),
                DiagnosticDescriptors.Semantic.Nav0122DifferentViewsInContinuationNotSupported);
        }
    }

}
