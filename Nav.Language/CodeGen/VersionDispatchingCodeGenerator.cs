#region Using Directives

using System;
using System.Collections.Generic;
using System.Collections.Immutable;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// Die Codegen-Weiche: wählt je <see cref="CodeGenerationUnit"/> den Generator ihrer
/// <see cref="NavLanguageVersion"/> und delegiert an ihn. Weil die Version ein Per-Datei-Fakt ist,
/// kann ein einziger Lauf Dateien verschiedener Generationen mischen; die passenden Generatoren
/// werden hier je Version einmalig erzeugt und wiederverwendet.
/// <para>
/// Derzeit ist ausschließlich <see cref="NavLanguageVersion.Version1"/> implementiert
/// (<see cref="CodeGeneratorV1"/>). Eine neue Generation reiht sich in <see cref="CreateGenerator"/>
/// ein — analog zu <see cref="NavCodeGenFacts.For"/>.
/// </para>
/// </summary>
sealed class VersionDispatchingCodeGenerator: ICodeGenerator {

    readonly GenerationOptions     _options;
    readonly IPathProviderFactory  _pathProviderFactory;

    readonly Dictionary<NavLanguageVersion, ICodeGenerator> _generators = new();

    public VersionDispatchingCodeGenerator(GenerationOptions options, IPathProviderFactory pathProviderFactory) {
        _options             = options             ?? throw new ArgumentNullException(nameof(options));
        _pathProviderFactory = pathProviderFactory ?? throw new ArgumentNullException(nameof(pathProviderFactory));
    }

    public ImmutableArray<CodeGenerationResult> Generate(CodeGenerationUnit codeGenerationUnit) {

        if (codeGenerationUnit == null) {
            throw new ArgumentNullException(nameof(codeGenerationUnit));
        }

        return GetGenerator(codeGenerationUnit.LanguageVersion).Generate(codeGenerationUnit);
    }

    ICodeGenerator GetGenerator(NavLanguageVersion version) {

        // Eine nicht unterstützte Version (z.B. #version 99) wird semantisch als Nav5001 gemeldet und
        // erreicht den Codegen ohnehin nicht (der Generator wirft bei Fehler-Diagnostics). Der Fallback
        // auf Default hält die Weiche dennoch robust — dieselbe Guard-Konvention wie NavCodeGenFacts.For.
        var effectiveVersion = version.IsSupported ? version : NavLanguageVersion.Default;

        if (!_generators.TryGetValue(effectiveVersion, out var generator)) {
            generator = CreateGenerator(effectiveVersion);
            _generators.Add(effectiveVersion, generator);
        }

        return generator;
    }

    ICodeGenerator CreateGenerator(NavLanguageVersion version) {

        if (version == NavLanguageVersion.Version1) {
            return new CodeGeneratorV1(_options, _pathProviderFactory);
        }

        // Unerreichbar: GetGenerator normalisiert auf eine unterstützte Version, und für jede
        // unterstützte Version muss hier ein Zweig stehen (Wächter gegen ein vergessenes Mapping
        // beim Freischalten einer neuen Generation).
        throw new NotSupportedException($"Für die Nav-Sprachversion {version} ist kein Codegenerator implementiert.");
    }

    public void Dispose() {

        foreach (var generator in _generators.Values) {
            generator.Dispose();
        }

        _generators.Clear();
    }

}
