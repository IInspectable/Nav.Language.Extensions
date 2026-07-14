using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer;

/// <summary>
/// Nav0122 (<c>Different views are not supported in a continuation</c>, Fehler): Alle Continuations,
/// die von <b>einer</b> Quelle aus erreichbar sind — transitiv über Choice-Ketten aufgelöst via
/// <see cref="EdgeExtensions.GetReachableContinuations"/> —, müssen auf <b>demselben</b> tragenden
/// GUI-Knoten sitzen: je Quelle entsteht genau eine mode-freie <c>Show…</c>-Aufruffläche, und die
/// kann die Continuation nur auf einen einzigen GUI-Knoten legen. Die Quellen sind dabei so gepoolt
/// wie die Call-Contexte des V2-Codegen: je Init-Knoten alle Ausgänge gemeinsam
/// (<see cref="IInitNodeSymbol.Outgoings"/>), jede Trigger-Transition für sich
/// (<see cref="ITaskDefinitionSymbol.TriggerTransitions"/>), je Task-Knoten alle Exit-Ausgänge
/// gemeinsam (<see cref="ITaskNodeSymbol.Outgoings"/>). Die Diagnose sitzt an der ersten beteiligten
/// GUI-Knoten-Referenz, die übrigen Referenzen sind Zusatz-Fundstellen. Ist die Continuation unter
/// der wirksamen Sprachversion (<see cref="CodeGenerationUnit.LanguageVersion"/>) gar nicht
/// verfügbar (<see cref="NavLanguageFeatures.IsAvailable"/>), schweigt diese Prüfung — die eine
/// treffende Diagnose ist dann das Versions-Gate
/// <see cref="Nav5000FeatureRequiresNavLanguageVersion"/>.
/// </summary>
public class Nav0122DifferentViewsInContinuationNotSupported: NavAnalyzer {

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0122DifferentViewsInContinuationNotSupported;

    /// <inheritdoc/>
    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        // Different views are not supported in a continuation
        //==============================
        // Aus einer Quelle heraus wird EINE mode-freie Show{View}-Methode generiert; sie kann die
        // Continuation nur auf GENAU EINEN tragenden GUI-Knoten legen. Erreichen die Continuations einer
        // Quelle (auch über Choices hinweg) verschiedene Views, ist das nicht abbildbar. Die „Quelle" ist
        // je nach Übergangsart unterschiedlich gepoolt:
        //
        // Ist die Continuation unter der effektiven #version gar nicht verfügbar, ist das Nav5000-Versions-Gate
        // die eine treffende Diagnose — die Struktur-Prüfung schweigt dann (Folgefehler unterdrücken).
        if (!NavLanguageFeatures.IsAvailable(NavLanguageFeature.Continuation,
                                             taskDefinition.CodeGenerationUnit?.LanguageVersion ?? NavLanguageVersion.Default)) {
            yield break;
        }

        // Init-Transitionen: alle Ausgänge eines Init-Knotens gemeinsam.
        foreach (var initNode in taskDefinition.NodeDeclarations.OfType<IInitNodeSymbol>()) {
            foreach (var diagnostic in Analyze(initNode.Outgoings)) {
                yield return diagnostic;
            }
        }

        // Trigger-Transitionen: jede für sich.
        foreach (var triggerTransition in taskDefinition.TriggerTransitions) {
            foreach (var diagnostic in Analyze(new IEdge[] {triggerTransition})) {
                yield return diagnostic;
            }
        }

        // Exit-Transitionen: alle Ausgänge eines Task-Knotens gemeinsam.
        foreach (var taskNode in taskDefinition.NodeDeclarations.OfType<ITaskNodeSymbol>()) {
            foreach (var diagnostic in Analyze(taskNode.Outgoings)) {
                yield return diagnostic;
            }
        }
    }

    /// <summary>
    /// Prüft einen Quellen-Pool: sammelt die tragenden GUI-Knoten aller von <paramref name="edges"/>
    /// aus erreichbaren Continuations (dedupliziert über die Knoten-Deklaration) und meldet die
    /// Kollision, sobald mehr als ein GUI-Knoten beteiligt ist.
    /// </summary>
    static IEnumerable<Diagnostic> Analyze(IEnumerable<IEdge> edges) {

        // In einer Continuation ist die Quelle immer ein GUI-Knoten (Nav0120); hier zählen die
        // verschiedenen tragenden GUI-Knoten.
        var guiNodeReferences = edges.SelectMany(edge => edge.GetReachableContinuations())
                                     .Select(continuation => continuation.SourceReference)
                                     .WhereNotNull()
                                     .DistinctBy(reference => reference.Declaration)
                                     .ToList();

        if (guiNodeReferences.Count > 1) {
            yield return new Diagnostic(
                guiNodeReferences[0].Location,
                guiNodeReferences.Skip(1).Select(reference => reference.Location),
                DiagnosticDescriptors.Semantic.Nav0122DifferentViewsInContinuationNotSupported);
        }
    }

}
