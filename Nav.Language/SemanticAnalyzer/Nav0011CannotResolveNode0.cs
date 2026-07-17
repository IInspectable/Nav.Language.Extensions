using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

/// <summary>
/// Nav0011 (<c>Cannot resolve node '{0}'</c>, Fehler): Jede Kante des Transitionsblocks muss auf
/// einen im Deklarationsblock deklarierten Zielknoten zeigen. Dieser Analyzer prüft die Zielseite
/// aller Kanten (<see cref="ITaskDefinitionSymbol.Edges"/>) auf Referenzen ohne aufgelöste
/// Deklaration (<see cref="INodeReferenceSymbol.Declaration"/> ist <c>null</c>), z.B.
/// <c>I1 --&gt; C;</c> ohne deklarierten Knoten <c>C</c>. Dieselbe Diagnose für unauflösbare
/// <b>Quell</b>knoten meldet bereits der Modellbau (<c>TaskDefinitionSymbolBuilder</c>) beim
/// Binden der Transitionen. Ausgenommen ist das deklarationslose <c>cancel</c>-Ziel
/// (<see cref="ICancelNodeReferenceSymbol"/>): dort ist die fehlende Deklaration gewollt (E4).
/// </summary>
public class Nav0011CannotResolveNode0: NavAnalyzer {

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0011CannotResolveNode0;

    /// <inheritdoc/>
    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        // Cannot resolve node '{0}'
        //==============================
        foreach (var targetReference in taskDefinition.Edges().Select(e => e.TargetReference)) {

            // Das cancel-Ziel hat per Design keine Deklaration (E4) — die fehlende Auflösung ist hier
            // gewollt und darf nicht als "unauflösbarer Name" gemeldet werden.
            if (targetReference is { Declaration: null } and not ICancelNodeReferenceSymbol) {
                yield return new Diagnostic(
                    targetReference.Location,
                    Descriptor,
                    targetReference.Name);
            }
        }
    }

}