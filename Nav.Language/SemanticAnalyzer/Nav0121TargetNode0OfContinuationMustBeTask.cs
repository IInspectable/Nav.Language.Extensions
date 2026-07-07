using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer;

public class Nav0121TargetNode0OfContinuationMustBeTask: NavAnalyzer {

    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0121TargetNode0OfContinuationMustBeTask;

    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        // The target node '{0}' of the continuation must be a task
        //==============================
        // Das Ziel einer Continuation (rechts von o-^/--^) muss ein Task-Knoten sein: .Begin{Task}(…) baut
        // .Concat(OpenModalTask/GotoTask(…)), was eine ITASK_BOUNDARY verlangt. Ein View-/Choice-/Exit-Ziel
        // hätte weder eine Begin-Fabrik noch ein Task-Boundary-Kommando. Unaufgelöste Knoten meldet bereits
        // Nav0011 — hier wird nur der aufgelöste Falschtyp gemeldet.
        foreach (var continuation in taskDefinition.Edges()
                                                   .OfType<IContinuableEdge>()
                                                   .Select(edge => edge.ContinuationTransition)
                                                   .WhereNotNull()) {

            var target = continuation.TargetReference;
            if (target?.Declaration is { } declaration and not ITaskNodeSymbol) {
                yield return new Diagnostic(
                    target.Location,
                    Descriptor,
                    declaration.Name);
            }
        }
    }

}
