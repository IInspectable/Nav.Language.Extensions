namespace Pharmatechnik.Nav.Language.CodeGen;

sealed class CodeGeneratorContext {

    public CodeGeneratorContext(CodeGeneratorV1 generator, NavLanguageVersion languageVersion) {
        Generator       = generator;
        LanguageVersion = languageVersion;
    }

    public CodeGeneratorV1 Generator       { get; }
    public string        ProductVersion  => MyAssembly.ProductVersion;
    public bool          NullableContext => Generator.Options.NullableContext;

    /// <summary>
    /// Die Sprach-Version der übersetzten <c>.nav</c>-Datei (aus <c>#version</c>, sonst
    /// <see cref="NavLanguageVersion.Default"/>). Durchreiche-Punkt in die CodeBuilder-Emitter, damit
    /// künftige Generationen versionsabhängig verzweigen können.
    /// </summary>
    public NavLanguageVersion LanguageVersion { get; }

}