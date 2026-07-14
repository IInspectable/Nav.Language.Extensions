using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

/// <summary>
/// Nav0024 (<c>An outgoing edge for exit '{0}' is already declared</c>, Fehler): Jeder
/// Exit-Verbindungspunkt eines eingebetteten Task-Knotens darf nur über genau eine
/// Exit-Transition verdrahtet werden:
/// <code>
/// t:e1 --&gt; e1;
/// t:e1 --&gt; e1;   // Nav0024
/// </code>
/// Geprüft je Task-Knoten mit aufgelöster Deklaration und mindestens einer Referenz im
/// Transitionsblock: Die aufgelösten
/// <see cref="IExitTransition.ExitConnectionPointReference"/>n seiner
/// <see cref="ITaskNodeSymbol.Outgoings"/> werden nach Namen gruppiert; jede Mehrfachgruppe wird
/// an ihrer ersten Verwendung gemeldet, die Duplikate sind zusätzliche Fundstellen.
/// </summary>
public class Nav0024OutgoingEdgeForExit0AlreadyDeclared: NavAnalyzer {

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0024OutgoingEdgeForExit0AlreadyDeclared;

    /// <inheritdoc/>
    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        //  An outgoing edge for exit '{0}' is already declared
        //==============================
        foreach (var taskNode in taskDefinition.NodeDeclarations.OfType<ITaskNodeSymbol>()) {

            if (taskNode.References.Any() && taskNode.Declaration != null) {

                var actualExits = taskNode.Outgoings
                                          .Select(et => et.ExitConnectionPointReference)
                                          .WhereNotNull()
                                          .ToList();

                foreach (var duplicates in actualExits.GroupBy(e => e.Name).Where(g => g.Count() > 1)) {
                    yield return new Diagnostic(
                        duplicates.First().Location,
                        duplicates.Skip(1).Select(d => d.Location),
                        Descriptor,
                        duplicates.First().Name);
                }

            }
        }
    }

}