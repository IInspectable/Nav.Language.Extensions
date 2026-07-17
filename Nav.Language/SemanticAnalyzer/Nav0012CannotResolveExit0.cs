using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

/// <summary>
/// Nav0012 (<c>Cannot resolve exit '{0}'</c>, Fehler): Der Name hinter dem Doppelpunkt einer
/// Exit-Transition muss ein <c>exit</c>-Verbindungspunkt der Task-Deklaration des Quellknotens
/// sein. Gemeldet für jede <see cref="IExitTransition.ExitConnectionPointReference"/>, die sich
/// nicht auflösen lässt (<see cref="IExitConnectionPointReferenceSymbol.Declaration"/> ist
/// <c>null</c>) — z.B. <c>B:e2 --&gt; e1;</c>, wenn Task <c>B</c> nur den Exit <c>e1</c>
/// deklariert; auch ein Verbindungspunkt anderer Art zählt nicht (<c>B:i1</c> mit
/// <c>init i1;</c> ist ebenfalls Nav0012).
/// </summary>
public class Nav0012CannotResolveExit0: NavAnalyzer {

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0012CannotResolveExit0;

    /// <inheritdoc/>
    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        // Cannot resolve exit '{0}'
        //==============================
        foreach (var exitTransition in taskDefinition.ExitTransitions) {

            if (exitTransition.ExitConnectionPointReference is { Declaration: null }) {
                yield return new Diagnostic(
                    exitTransition.ExitConnectionPointReference.Location,
                    Descriptor,
                    exitTransition.ExitConnectionPointReference.Name);

            }
        }

    }

}