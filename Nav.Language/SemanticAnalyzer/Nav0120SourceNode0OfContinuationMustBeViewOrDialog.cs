using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer;

public class Nav0120SourceNode0OfContinuationMustBeViewOrDialog: NavAnalyzer {

    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0120SourceNode0OfContinuationMustBeViewOrDialog;

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
