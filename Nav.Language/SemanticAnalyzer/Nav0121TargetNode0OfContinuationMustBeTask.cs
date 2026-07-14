using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer;

/// <summary>
/// Nav0121 (<c>The target node '{0}' of the continuation must be a task</c>, Fehler): Das Ziel
/// einer Continuation (rechts von <c>o-^</c>/<c>--^</c>,
/// <see cref="IEdge.TargetReference"/> der <see cref="IContinuationTransition"/>) muss ein
/// Task-Knoten (<see cref="ITaskNodeSymbol"/>) sein — die Continuation setzt den Aufruf des
/// Folge-Tasks per <c>.Concat(…)</c> auf das GUI-Kommando des tragenden Knotens; ein
/// View-/Choice-/Exit-Ziel hätte weder eine Begin-Fabrik noch ein Task-Boundary-Kommando.
/// Gemeldet wird nur der <b>aufgelöste</b> Falschtyp (die Diagnose sitzt an der Zielreferenz der
/// Continuation); unauflösbare Knoten meldet bereits <see cref="Nav0011CannotResolveNode0"/>.
/// Ist die Continuation unter der wirksamen Sprachversion
/// (<see cref="CodeGenerationUnit.LanguageVersion"/>) gar nicht verfügbar
/// (<see cref="NavLanguageFeatures.IsAvailable"/>), schweigt diese Prüfung — die eine treffende
/// Diagnose ist dann das Versions-Gate <see cref="Nav5000FeatureRequiresNavLanguageVersion"/>.
/// Das Gegenstück auf der Quellseite ist
/// <see cref="Nav0120SourceNode0OfContinuationMustBeViewOrDialog"/>.
/// </summary>
public class Nav0121TargetNode0OfContinuationMustBeTask: NavAnalyzer {

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0121TargetNode0OfContinuationMustBeTask;

    /// <inheritdoc/>
    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        // The target node '{0}' of the continuation must be a task
        //==============================
        // Das Ziel einer Continuation (rechts von o-^/--^) muss ein Task-Knoten sein: .Begin{Task}(…) baut
        // .Concat(OpenModalTask/GotoTask(…)), was eine ITASK_BOUNDARY verlangt. Ein View-/Choice-/Exit-Ziel
        // hätte weder eine Begin-Fabrik noch ein Task-Boundary-Kommando. Unaufgelöste Knoten meldet bereits
        // Nav0011 — hier wird nur der aufgelöste Falschtyp gemeldet.
        //
        // Ist die Continuation unter der effektiven #version gar nicht verfügbar, ist das Nav5000-Versions-Gate
        // die eine treffende Diagnose — die Struktur-Prüfung schweigt dann (Folgefehler unterdrücken).
        if (!NavLanguageFeatures.IsAvailable(NavLanguageFeature.Continuation,
                                             taskDefinition.CodeGenerationUnit?.LanguageVersion ?? NavLanguageVersion.Default)) {
            yield break;
        }

        foreach (var continuation in taskDefinition.Edges()
                                                   .OfType<IContinuableEdge>()
                                                   .Select(edge => edge.ContinuationTransition)
                                                   .WhereNotNull()) {

            var target = continuation.TargetReference;
            if (target?.Declaration is { } declaration and not ITaskNodeSymbol) {
                yield return new Diagnostic(
                    target.Location,
                    Descriptor,
                    declaration.Name);
            }
        }
    }

}
