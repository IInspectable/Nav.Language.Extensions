#region Using Directives

using System;
using System.Collections.Immutable;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// Factory für einen <see cref="ICodeGenerator"/>. Entkoppelt die Codegen-Pipeline
/// (<c>NavCodeGeneratorPipeline</c>) von der konkreten Generator-Implementierung und erlaubt so,
/// den Generator in Tests auszutauschen; im Produktivpfad liefert
/// <see cref="CodeGeneratorProvider.Default"/> stets die Versions-Weiche
/// <see cref="VersionDispatchingCodeGenerator"/>.
/// </summary>
public interface ICodeGeneratorProvider {

    /// <summary>
    /// Erzeugt einen <see cref="ICodeGenerator"/> für die gegebenen <paramref name="options"/> und
    /// die Pfad-Factory <paramref name="pathProviderFactory"/> (letztere bestimmt die Zielpfade der
    /// erzeugten Artefakte).
    /// </summary>
    ICodeGenerator Create(GenerationOptions options, IPathProviderFactory pathProviderFactory);

}

/// <summary>
/// Die Standard-Factory für den Codegenerator. Als zustandsloser Singleton
/// (<see cref="Default"/>) ausgelegt; jeder <see cref="Create"/>-Aufruf liefert einen frischen,
/// versions-dispatchenden Generator.
/// </summary>
public sealed class CodeGeneratorProvider: ICodeGeneratorProvider {

    CodeGeneratorProvider() {

    }

    /// <summary>Der prozessweit geteilte Standard-Provider.</summary>
    public static readonly ICodeGeneratorProvider Default = new CodeGeneratorProvider();

    /// <summary>
    /// Erzeugt den produktiven Generator: die Versions-Weiche
    /// <see cref="VersionDispatchingCodeGenerator"/>, die je <see cref="CodeGenerationUnit"/> an den
    /// Generator ihrer Sprach-Generation delegiert.
    /// </summary>
    public ICodeGenerator Create(GenerationOptions options, IPathProviderFactory pathProviderFactory) {
        // Die Weiche zwischen den Sprach-Generationen liegt hinter dieser Factory: der Dispatcher wählt
        // je CodeGenerationUnit den Generator ihrer Version.
        return new VersionDispatchingCodeGenerator(options, pathProviderFactory);
    }

}

/// <summary>
/// Der versionsübergreifende Vertrag der Codegenerierung: übersetzt eine bereits geparste und
/// semantisch modellierte <see cref="CodeGenerationUnit"/> in die zu schreibenden C#-Artefakte,
/// ohne dass der Aufrufer die konkrete Sprach-Generation kennt. <see cref="IDisposable"/>, weil
/// Implementierungen (z.B. StringTemplate-Gruppen) native Ressourcen halten können.
/// </summary>
public interface ICodeGenerator: IDisposable {

    /// <summary>
    /// Generiert für jede Task-Definition der <paramref name="codeGenerationUnit"/> deren
    /// Artefakte als <see cref="CodeGenerationResult"/> (je eine Spec-Liste). Die Weiche zwischen
    /// den Sprach-Generationen liegt hinter dieser Schnittstelle.
    /// </summary>
    ImmutableArray<CodeGenerationResult> Generate(CodeGenerationUnit codeGenerationUnit);

}
