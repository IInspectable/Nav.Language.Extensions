using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

/// <summary>
/// Nav0025 (<c>No outgoing edge declared for exit '{0}'</c>, Fehler): Für jeden
/// Exit-Verbindungspunkt der Deklaration eines eingebetteten Task-Knotens muss eine
/// Exit-Transition deklariert sein — jeder Ausgang des Unter-Tasks braucht eine Fortsetzung im
/// umgebenden Workflow. Deklariert Task <c>C</c> etwa die Exits <c>e1</c> und <c>e2</c>, meldet
/// <c>task C;</c> ohne <c>C:e1 --&gt; …</c> und <c>C:e2 --&gt; …</c> je Exit ein Nav0025. Die
/// offenen Exits liefert <see cref="TaskNodeSymbolExtensions.GetUnconnectedExits"/>; Task-Knoten
/// ohne Referenz im Transitionsblock werden übergangen (die behandelt Nav1012 als „not
/// required"). Die Diagnose sitzt an der Knoten-Deklaration, die Zielreferenzen der eingehenden
/// Kanten sind zusätzliche Fundstellen.
/// </summary>
public class Nav0025NoOutgoingEdgeForExit0Declared: NavAnalyzer {

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0025NoOutgoingEdgeForExit0Declared;

    /// <inheritdoc/>
    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        //  No outgoing edge declared for exit '{0}'
        //==============================
        foreach (var taskNode in taskDefinition.NodeDeclarations.OfType<ITaskNodeSymbol>()) {

            // Wird mit Nav1012TaskNode0NotRequired behandelt
            if (!taskNode.References.Any()) {
                continue;
            }

            foreach (var expectedExit in taskNode.GetUnconnectedExits()) {

                yield return new Diagnostic(
                    taskNode.Location,
                    taskNode.Incomings
                            .Select(edge => edge.TargetReference)
                            .WhereNotNull()
                            .Select(nodeReference => nodeReference.Location),
                    Descriptor,
                    expectedExit.Name);
            }
        }

    }

}