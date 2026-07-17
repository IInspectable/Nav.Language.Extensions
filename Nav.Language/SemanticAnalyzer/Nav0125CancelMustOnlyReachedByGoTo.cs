using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer;

/// <summary>
/// Nav0125 (<c>Cancel can only be reached by a goto edge (--&gt;)</c>, Fehler): Das
/// <c>cancel</c>-Kantenziel (<see cref="ICancelNodeReferenceSymbol"/>) darf nur über eine Goto-Kante
/// (<see cref="EdgeMode.Goto"/>, <c>--&gt;</c>) erreicht werden; modale (<c>o-&gt;</c>) und
/// nicht-modale (<c>==&gt;</c>) Kanten auf <c>cancel</c> sind Fehler, z.B.
/// <c>V o-&gt; cancel on Trigger;</c>. Der grammatische Zwilling ist <c>end</c>
/// (<see cref="Nav0106EndNode0MustOnlyReachedByGoTo"/>); anders als dort trägt <c>cancel</c> keinen
/// Namen (E4), die Meldung nennt daher keinen Knotennamen. Die Diagnose sitzt am Kanten-Operator
/// (<see cref="IEdgeModeSymbol"/>); Kanten ohne Operator werden übergangen. Ist <c>cancel</c> unter
/// der wirksamen Sprachversion (<see cref="CodeGenerationUnit.LanguageVersion"/>) gar nicht verfügbar
/// (<see cref="NavLanguageFeatures.IsAvailable"/>), schweigt diese Prüfung — die eine treffende
/// Diagnose ist dann das Versions-Gate <see cref="Nav5000FeatureRequiresNavLanguageVersion"/>.
/// </summary>
public class Nav0125CancelMustOnlyReachedByGoTo: NavAnalyzer {

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0125CancelMustOnlyReachedByGoTo;

    /// <inheritdoc/>
    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        // Cancel can only be reached by a goto edge (-->)
        //==============================
        // Ist cancel unter der effektiven #version gar nicht verfügbar, ist das Nav5000-Versions-Gate die
        // eine treffende Diagnose — diese Prüfung schweigt dann (Folgefehler unterdrücken).
        if (!NavLanguageFeatures.IsAvailable(NavLanguageFeature.Cancel,
                                             taskDefinition.CodeGenerationUnit?.LanguageVersion ?? NavLanguageVersion.Default)) {
            yield break;
        }

        foreach (var edge in taskDefinition.Edges().Where(e => e.TargetsCancel())) {

            if (edge.EdgeMode          != null &&
                edge.EdgeMode.EdgeMode != EdgeMode.Goto) {

                yield return new Diagnostic(
                    edge.EdgeMode.Location,
                    Descriptor);
            }
        }
    }

}
