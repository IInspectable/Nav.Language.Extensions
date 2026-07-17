using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer;

/// <summary>
/// Nav5001 (<c>Nav language version {0} is not supported by this engine; the highest supported
/// version is {1}</c>, Fehler): Meldet eine wohlgeformte, aber von der Engine <b>nicht
/// unterstützte</b> Sprach-Version. Der
/// <see cref="NavDirectiveParser"/> akzeptiert bewusst jede syntaktisch gültige, nicht-negative
/// Versionszahl (ein fehlerhafter Wert ist bereits <c>Nav3002</c>); ob die Engine diese Version
/// <i>kennt</i>, ist eine rein semantische Frage — parallel zum Feature-Gate <c>Nav5000</c>. Die Menge
/// der gültigen Versionen ist zentral in <see cref="NavLanguageVersion.SupportedVersions"/> hinterlegt.
/// Geprüft wird nur die <b>wirksame</b> Direktive
/// (<see cref="CodeGenerationUnitSyntax.LanguageVersionDirective"/>) — eine deplatzierte, unwirksame
/// Direktive ist bereits per <c>Nav3003</c> gemeldet. Die Diagnose überspannt die gesamte
/// <c>#version</c>-Direktive und nennt neben der unbekannten Version die höchste unterstützte
/// (<see cref="NavLanguageVersion.Latest"/>).
/// </summary>
public class Nav5001NavLanguageVersionNotSupported: NavAnalyzer {

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav5001NavLanguageVersion0NotSupported1IsLatest;

    /// <inheritdoc cref="INavAnalyzer.Analyze"/>
    /// <remarks>
    /// Überschreibt bewusst den Einstieg auf Unit-Ebene, statt auf die Task-Overloads aufzufächern:
    /// die Versions-Direktive ist eine Eigenschaft der Datei, nicht eines einzelnen Tasks.
    /// </remarks>
    public override IEnumerable<Diagnostic> Analyze(CodeGenerationUnit codeGenerationUnit, AnalyzerContext context) {
        //==============================
        // Nav language version not supported
        //==============================
        var directive = codeGenerationUnit.Syntax.LanguageVersionDirective;
        if (directive != null && !directive.Version.IsSupported) {
            yield return new Diagnostic(
                directive.GetLocation(),
                Descriptor,
                directive.Version,
                NavLanguageVersion.Latest);
        }
    }

}
