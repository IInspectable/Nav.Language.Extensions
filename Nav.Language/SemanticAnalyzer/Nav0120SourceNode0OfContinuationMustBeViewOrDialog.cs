using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer;

/// <summary>
/// Nav0120 (<c>The source node '{0}' of the continuation must be a view or dialog</c>, Fehler):
/// Der tragende Knoten einer Continuation (<c>… o-^ Task</c> / <c>… --^ Task</c>) muss ein
/// GUI-Knoten (View oder Dialog, <see cref="IGuiNodeSymbol"/>) sein. Die Quelle der Continuation
/// (<see cref="IEdge.SourceReference"/> der <see cref="IContinuationTransition"/>) ist der
/// Zielknoten der umgebenden Transition — nur ein GUI-Knoten baut das GUI-Kommando, auf dem der
/// Folge-Task per <c>.Concat(…)</c> aufsetzt; ein Task-/Choice-/Exit-Knoten kann keine Continuation
/// tragen. Gemeldet wird nur der <b>aufgelöste</b> Falschtyp (die Diagnose sitzt an der Quellreferenz
/// der Continuation); unauflösbare Knoten meldet bereits <see cref="Nav0011CannotResolveNode0"/>.
/// Ist die Continuation unter der wirksamen Sprachversion
/// (<see cref="CodeGenerationUnit.LanguageVersion"/>) gar nicht verfügbar
/// (<see cref="NavLanguageFeatures.IsAvailable"/>), schweigt diese Prüfung — die eine treffende
/// Diagnose ist dann das Versions-Gate <see cref="Nav5000FeatureRequiresNavLanguageVersion"/>.
/// Das Gegenstück auf der Zielseite ist <see cref="Nav0121TargetNode0OfContinuationMustBeTask"/>.
/// </summary>
public class Nav0120SourceNode0OfContinuationMustBeViewOrDialog: NavAnalyzer {

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0120SourceNode0OfContinuationMustBeViewOrDialog;

    /// <inheritdoc/>
    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        // The source node '{0}' of the continuation must be a view or dialog
        //==============================
        // Der tragende Knoten einer Continuation (… o-^/--^ Task) — der Zielknoten der umgebenden
        // Transition — muss ein GUI-Knoten (View oder Dialog) sein: nur er baut das GOTO_GUI/OPEN_MODAL_GUI,
        // auf dem der Folge-Task via .Concat(…) sitzt. Ein Task-/Choice-/Exit-Knoten kann keine Continuation
        // tragen. Unaufgelöste Knoten meldet bereits Nav0011 — hier wird nur der aufgelöste Falschtyp gemeldet.
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

            var source = continuation.SourceReference;
            if (source?.Declaration is { } declaration and not IGuiNodeSymbol) {
                yield return new Diagnostic(
                    source.Location,
                    Descriptor,
                    declaration.Name);
            }
        }
    }

}
