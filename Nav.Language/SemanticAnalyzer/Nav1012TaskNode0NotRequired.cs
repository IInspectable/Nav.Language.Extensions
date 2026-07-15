using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer;

/// <summary>
/// Nav1012 (<c>The task node '{0}' is not required by the code and can be safely removed</c>,
/// Dead-Code-Hinweis der Kategorie <see cref="DiagnosticCategory.DeadCode"/>): Ein
/// <c>task</c>-Knoten (<see cref="ITaskNodeSymbol"/>), auf den keine Referenz aus dem
/// Transitionsblock existiert (<see cref="INodeSymbol.References"/>), ist überflüssig — der
/// eingebundene Task wird weder aufgerufen noch fortgesetzt, z.B. <c>task C;</c> ohne jede Kante
/// von oder auf <c>C</c>. Gemeldet wird über die gesamte Knoten-Deklaration; solche
/// unreferenzierten Task-Knoten überspringt <see cref="Nav0025NoOutgoingEdgeForExit0Declared"/>
/// deshalb bewusst. Als einziger Dead-Code-Analyzer per Quelltext-Kommentar abschaltbar:
/// <c>task C; // disable Nav1012</c> unterdrückt die Diagnose
/// (<see cref="AnalyzerContext.IsWarningDisabled"/>).
/// </summary>
public class Nav1012TaskNode0NotRequired: NavAnalyzer {

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.DeadCode.Nav1012TaskNode0NotRequired;

    /// <inheritdoc/>
    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        //  The task node '{0}' is not required by the code and can be safely removed
        //==============================
        foreach (var taskNode in taskDefinition.NodeDeclarations.OfType<ITaskNodeSymbol>()) {

            if (context.IsWarningDisabled(taskNode, Descriptor)) {
                continue;
            }

            if (!taskNode.References.Any()) {

                yield return new Diagnostic(
                    taskNode.Syntax.GetLocation(),
                    Descriptor,
                    taskNode.Name);
            }
        }

    }

}