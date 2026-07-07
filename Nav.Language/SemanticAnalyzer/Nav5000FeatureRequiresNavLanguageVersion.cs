using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer;

/// <summary>
/// Das Versions-Gate der Version-2-Konstrukte: Continuation-Kanten (<c>o-^</c>/<c>--^</c>) und
/// Choice-Parameter (<c>choice X [params …]</c>) sind erst ab <see cref="NavLanguageVersion.Version2"/>
/// zulässig. Der Parser bleibt bewusst permissiv (er kennt stets die volle Syntax); die
/// Versions-Abhängigkeit ist eine rein semantische Prüfung — so entsteht statt eines kryptischen
/// Parse-Fehlers eine treffende <c>Nav5000</c>-Meldung samt Handlungsanweisung (<c>#version 2</c>
/// ergänzen). Einzige Autorität für „welches Feature ab welcher Version" ist
/// <see cref="NavLanguageFeatures"/>; ist ein Feature unter der effektiven Version nicht verfügbar,
/// ist diese Meldung die <b>eine treffende</b> Diagnose — die Continuation-Struktur-Analyzer
/// (<c>Nav0120</c>/<c>0121</c>/<c>0122</c>) schweigen dann (Folgefehler unterdrücken).
/// </summary>
public class Nav5000FeatureRequiresNavLanguageVersion: NavAnalyzer {

    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav5000Feature0RequiresNavLanguageVersion1;

    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {

        var version = taskDefinition.CodeGenerationUnit?.LanguageVersion ?? NavLanguageVersion.Default;

        // Continuation-Kanten (o-^/--^): eine Meldung je Kante, verankert am Fortsetzungs-Kantenmodus (dem
        // Keyword selbst); fehlt dieser, am gesamten Continuation-Syntaxknoten.
        foreach (var continuation in taskDefinition.Edges()
                                                   .OfType<IContinuableEdge>()
                                                   .Select(edge => edge.ContinuationTransition)
                                                   .WhereNotNull()) {

            if (Gate(NavLanguageFeature.Continuation, version, continuation.EdgeMode?.Location ?? continuation.Location) is {} diagnostic) {
                yield return diagnostic;
            }
        }

        // Choice-Parameter (choice X [params …]): eine Meldung je Klausel, verankert an der [params …]-Klausel.
        foreach (var choice in taskDefinition.NodeDeclarations.OfType<IChoiceNodeSymbol>()) {
            if (choice.Syntax.CodeParamsDeclaration is {} codeParams &&
                Gate(NavLanguageFeature.ChoiceParameters, version, codeParams.GetLocation()) is {} diagnostic) {
                yield return diagnostic;
            }
        }
    }

    // Liefert die Nav5000-Diagnose, wenn das Feature unter der effektiven Version nicht verfügbar ist — sonst null.
    Diagnostic? Gate(NavLanguageFeature feature, NavLanguageVersion version, Location location) {

        var required = NavLanguageFeatures.RequiredVersion(feature);
        if (version >= required) {
            return null;
        }

        return new Diagnostic(location, Descriptor, feature, required);
    }

}
