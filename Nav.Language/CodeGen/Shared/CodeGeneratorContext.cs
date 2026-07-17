namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// Der generationsübergreifende Emitter-Kontext: reicht die Datei-Sprachversion und die
/// generierungsrelevanten <see cref="GenerationOptions"/>-Fakten (Nullable-Kontext, Produktversion)
/// in die CodeBuilder-Emitter durch. Bewusst <b>nicht</b> an einen konkreten Generator gekoppelt —
/// V1 (<see cref="CodeGeneratorV1"/>) wie V2 (<c>CodeGeneratorV2</c>) erzeugen sich denselben Kontext.
/// </summary>
sealed class CodeGeneratorContext {

    public CodeGeneratorContext(GenerationOptions options, NavLanguageVersion languageVersion) {
        Options         = options;
        LanguageVersion = languageVersion;
    }

    /// <summary>Die dieser Erzeugung zugrunde liegenden <see cref="GenerationOptions"/>.</summary>
    public GenerationOptions Options         { get; }
    /// <summary>
    /// Die Produktversion (aus <see cref="MyAssembly.ProductVersion"/>), die die Emitter in den erzeugten
    /// Datei-Header schreiben.
    /// </summary>
    public string            ProductVersion  => MyAssembly.ProductVersion;
    /// <summary>
    /// Ob der erzeugte C#-Code im aktivierten Nullable-Kontext stehen soll (Durchreiche von
    /// <see cref="GenerationOptions.NullableContext"/>).
    /// </summary>
    public bool              NullableContext => Options.NullableContext;

    /// <summary>
    /// Die Sprach-Version der übersetzten <c>.nav</c>-Datei (aus <c>#version</c>, sonst
    /// <see cref="NavLanguageVersion.Default"/>). Durchreiche-Punkt in die CodeBuilder-Emitter, damit
    /// Generationen versionsabhängig verzweigen können.
    /// </summary>
    public NavLanguageVersion LanguageVersion { get; }

}
