using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

/// <summary>
/// Nav0103 (<c>An init node must not contain incoming edges</c>, Fehler): Ein <c>init</c>-Knoten
/// (<see cref="IInitNodeSymbol"/>) ist reiner Einstiegspunkt des Tasks und darf nicht Ziel einer
/// Kante sein — er ist bewusst kein <see cref="ITargetNodeSymbol"/>. Dieser Analyzer prüft die
/// Zielseite aller Kanten (<see cref="ITaskDefinitionSymbol.Edges"/>) — Transitionen wie
/// Exit-Transitionen — auf Init-Deklarationen, z.B. <c>A --&gt; I1 on Foo;</c> oder
/// <c>A:e1 --&gt; I1;</c>. Die Diagnose sitzt an der Zielreferenz.
/// </summary>
public class Nav0103InitNodeMustNotContainIncomingEdges: NavAnalyzer {

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0103InitNodeMustNotContainIncomingEdges;

    /// <inheritdoc/>
    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        // An init node must not contain incoming edges
        //==============================
        foreach (var targetReference in taskDefinition.Edges().Select(e => e.TargetReference)) {

            if (targetReference?.Declaration is IInitNodeSymbol) {
                yield return (new Diagnostic(
                    targetReference.Location,
                    Descriptor,
                    targetReference.Name));
            }
        }
    }

}