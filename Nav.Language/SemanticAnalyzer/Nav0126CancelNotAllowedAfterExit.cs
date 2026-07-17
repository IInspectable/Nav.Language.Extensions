using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer;

/// <summary>
/// Nav0126 (<c>Cancel is not allowed as the target of an exit transition</c>, Fehler): Das
/// <c>cancel</c>-Kantenziel (<see cref="ICancelNodeReferenceSymbol"/>) ist nur an einer Init-,
/// Trigger- oder Choice-Transition zulässig (der bedingte Cancel am Choice-Arm, der unbedingte
/// Swallow an einer direkten Init-/Trigger-Kante, E5), <b>nicht</b> als Ziel einer Exit-Transition
/// (<see cref="IExitTransition"/>, z.B. <c>Sub:e --&gt; cancel;</c>). Cancel bedeutet „die Logik gibt
/// nichts zurück" und entsteht ausschließlich in <c>{X}Logic</c>-Methoden von Init-/Trigger-/Choice-
/// Kanten (E6); eine Exit-Transition trägt keine solche Logik, ein Cancel-Ausgang wäre dort ohne
/// Wirkung. Die Diagnose sitzt am <c>cancel</c>-Keyword (der Zielreferenz). Ist <c>cancel</c> unter
/// der wirksamen Sprachversion (<see cref="CodeGenerationUnit.LanguageVersion"/>) gar nicht verfügbar
/// (<see cref="NavLanguageFeatures.IsAvailable"/>), schweigt diese Prüfung — die eine treffende
/// Diagnose ist dann das Versions-Gate <see cref="Nav5000FeatureRequiresNavLanguageVersion"/>.
/// </summary>
public class Nav0126CancelNotAllowedAfterExit: NavAnalyzer {

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0126CancelNotAllowedAfterExit;

    /// <inheritdoc/>
    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        // Cancel is not allowed as the target of an exit transition
        //==============================
        // Ist cancel unter der effektiven #version gar nicht verfügbar, ist das Nav5000-Versions-Gate die
        // eine treffende Diagnose — diese Prüfung schweigt dann (Folgefehler unterdrücken).
        if (!NavLanguageFeatures.IsAvailable(NavLanguageFeature.Cancel,
                                             taskDefinition.CodeGenerationUnit?.LanguageVersion ?? NavLanguageVersion.Default)) {
            yield break;
        }

        foreach (var exitTransition in taskDefinition.ExitTransitions.Where(t => t.TargetsCancel())) {

            yield return new Diagnostic(
                exitTransition.TargetReference!.Location,
                Descriptor);
        }
    }

}
