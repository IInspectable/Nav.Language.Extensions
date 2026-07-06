#region Using Directives

using System;
using System.Collections.Immutable;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

public interface ICodeGeneratorProvider {

    ICodeGenerator Create(GenerationOptions options, IPathProviderFactory pathProviderFactory);

}

public sealed class CodeGeneratorProvider: ICodeGeneratorProvider {

    CodeGeneratorProvider() {

    }

    public static readonly ICodeGeneratorProvider Default = new CodeGeneratorProvider();

    public ICodeGenerator Create(GenerationOptions options, IPathProviderFactory pathProviderFactory) {
        // Die Weiche zwischen den Sprach-Generationen liegt hinter dieser Fabrik: der Dispatcher wählt
        // je CodeGenerationUnit den Generator ihrer Version.
        return new VersionDispatchingCodeGenerator(options, pathProviderFactory);
    }

}

public interface ICodeGenerator: IDisposable {

    /// <summary>
    /// Generiert für jede Task-Definition der <paramref name="codeGenerationUnit"/> deren
    /// Artefakte als <see cref="CodeGenerationResult"/> (je eine Spec-Liste). Die Weiche zwischen
    /// den Sprach-Generationen liegt hinter dieser Schnittstelle.
    /// </summary>
    ImmutableArray<CodeGenerationResult> Generate(CodeGenerationUnit codeGenerationUnit);

}
