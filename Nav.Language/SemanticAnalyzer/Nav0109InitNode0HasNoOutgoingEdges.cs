using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

/// <summary>
/// Nav0109 (<c>The init node '{0}' has no outgoing edges</c>, Warnung): Ein <c>init</c>-Knoten
/// (<see cref="IInitNodeSymbol"/>) ohne ausgehende Init-Transition
/// (<see cref="IInitNodeSymbol.Outgoings"/>) ist ein Einstiegspunkt ohne Fortsetzung, z.B.
/// <c>init I2;</c> ohne <c>I2 --&gt; …</c>. Enthält die Task-Definition überhaupt keine Kanten
/// (reiner Deklarationsrumpf), unterbleibt die Warnung. Die Diagnose sitzt am Alias
/// (<see cref="IInitNodeSymbol.Alias"/>), falls vorhanden, sonst an der <c>init</c>-Deklaration.
/// </summary>
public class Nav0109InitNode0HasNoOutgoingEdges: NavAnalyzer {

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0109InitNode0HasNoOutgoingEdges;

    /// <inheritdoc/>
    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        //  The init node '{0}' has no outgoing edges
        //==============================
        // Wenn überhaupt keine Edges definiert sind, werten wir bisweilen unbenutzte Verbindungspunkte nicht als Warnung
        if (!taskDefinition.Edges().Any()) {
            yield break;
        }
        foreach (var initNode in taskDefinition.NodeDeclarations.OfType<IInitNodeSymbol>()) {

            if (!initNode.Outgoings.Any()) {

                yield return new Diagnostic(
                    initNode.Alias?.Location ?? initNode.Location,
                    Descriptor,
                    initNode.Name);
            }
        }

    }

}