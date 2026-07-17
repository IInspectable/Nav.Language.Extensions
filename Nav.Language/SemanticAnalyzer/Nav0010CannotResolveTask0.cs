using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

/// <summary>
/// Nav0010 (<c>Cannot resolve task '{0}'</c>, Fehler): Ein verwendeter Task-Name muss auf eine
/// bekannte Task-Deklaration auflösbar sein. Gemeldet an zwei Stellen: an jeder
/// Task-Knoten-Deklaration ohne auflösbare Deklaration
/// (<see cref="ITaskNodeSymbol.Declaration"/> ist <c>null</c>), z.B. <c>task C;</c> ohne einen
/// Task <c>C</c> — sowie an der Quellseite jeder Exit-Transition, deren Name vor dem Doppelpunkt
/// sich nicht auf einen deklarierten Task-Knoten auflöst
/// (<see cref="IExitTransition.TaskNodeSourceReference"/>), z.B. <c>C:e --&gt; e1;</c> ohne
/// <c>task C;</c> im Deklarationsblock.
/// </summary>
public class Nav0010CannotResolveTask0: NavAnalyzer {

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0010CannotResolveTask0;

    /// <inheritdoc/>
    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        // Cannot resolve task '{0}'
        //==============================
        foreach (var taskNode in taskDefinition.NodeDeclarations.OfType<ITaskNodeSymbol>()) {
            if (taskNode.Declaration == null) {

                yield return (new Diagnostic(
                    taskNode.Location,
                    Descriptor,
                    taskNode.Name));
            }
        }

        // Bei einer Exit-Transition muss der Knoten vor dem Verbindungspunkt ein Task sein
        foreach (var exitTransition in taskDefinition.ExitTransitions) {

            if (exitTransition.TaskNodeSourceReference is { Declaration: null }) {

                yield return new Diagnostic(
                    exitTransition.TaskNodeSourceReference.Location,
                    Descriptor,
                    exitTransition.TaskNodeSourceReference.Name);

            }
        }
    }

}