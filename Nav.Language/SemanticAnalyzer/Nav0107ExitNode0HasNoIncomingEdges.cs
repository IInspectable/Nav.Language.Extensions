using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

/// <summary>
/// Nav0107 (<c>The exit node '{0}' has no incoming edges</c>, Warnung): Ein deklarierter
/// <c>exit</c>-Knoten (<see cref="IExitNodeSymbol"/>) ohne eingehende Kanten
/// (<see cref="ITargetNodeSymbol.Incomings"/>) ist ein toter Ausgang — keine Kante des
/// Transitionsblocks führt zu ihm. Gemeldet wird an der Exit-Deklaration. Zwei Ausnahmen:
/// Enthält die Task-Definition überhaupt keine Kanten (reiner Deklarationsrumpf), unterbleibt
/// die Warnung ganz; außerdem lässt sie sich per <c>// disable Nav0107</c>-Kommentar an der
/// Deklaration unterdrücken (<see cref="AnalyzerContext.IsWarningDisabled"/>).
/// </summary>
public class Nav0107ExitNode0HasNoIncomingEdges: NavAnalyzer {

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0107ExitNode0HasNoIncomingEdges;

    /// <inheritdoc/>
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