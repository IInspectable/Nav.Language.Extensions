namespace Pharmatechnik.Nav.Language.CodeGen;

sealed class CodeGeneratorContext {

    public CodeGeneratorContext(CodeGenerator generator, NavLanguageVersion languageVersion) {
        Generator       = generator;
        LanguageVersion = languageVersion;
    }

    public CodeGenerator Generator       { get; }
    public string        ProductVersion  => MyAssembly.ProductVersion;
    public bool          NullableContext => Generator.Options.NullableContext;

    /// <summary>
    /// Die Sprach-Version der übersetzten <c>.nav</c>-Datei (aus <c>#pragma version</c>, sonst
    /// <see cref="NavLanguageVersion.Default"/>). Durchreiche-Punkt in die StringTemplates, damit künftige
    /// Templates versionsabhängig verzweigen können.
    /// </summary>
    public NavLanguageVersion LanguageVersion { get; }

}