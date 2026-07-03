using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer;

/// <summary>
/// Meldet eine wohlgeformte, aber von der Engine <b>nicht unterstützte</b> Sprach-Version. Der
/// <see cref="NavDirectiveParser"/> akzeptiert bewusst jede syntaktisch gültige, nicht-negative
/// Versionszahl (ein fehlerhafter Wert ist bereits <c>Nav3002</c>); ob die Engine diese Version
/// <i>kennt</i>, ist eine rein semantische Frage — parallel zum Feature-Gate <c>Nav5000</c>. Die Menge
/// der gültigen Versionen ist zentral in <see cref="NavLanguageVersion.SupportedVersions"/> hinterlegt.
/// </summary>
public class Nav5001NavLanguageVersionNotSupported: NavAnalyzer {

    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav5001NavLanguageVersion0NotSupported1IsLatest;

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
