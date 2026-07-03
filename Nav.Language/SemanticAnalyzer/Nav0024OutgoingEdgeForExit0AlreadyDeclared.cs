using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

public class Nav0024OutgoingEdgeForExit0AlreadyDeclared: NavAnalyzer {

    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0024OutgoingEdgeForExit0AlreadyDeclared;

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