using System;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Pharmatechnik.Nav.Language.Analyzer.SourceGenerator.Tests.Shared;

/// <summary>
/// Basis für die Generator-Tests: baut eine In-Memory-Compilation aus Quelltext und lässt den
/// Registry-Generator darüber laufen. Der Generator arbeitet <b>semantisch</b> (implementierte
/// Interfaces, Konstruktoren) — der Test-Quelltext muss daher die Minimalbasis
/// (<c>INavAnalyzer</c>) selbst mitbringen.
/// </summary>
public abstract class GeneratorTestBase {

    protected static CSharpCompilation CreateCompilation(string source) {
        return CSharpCompilation.Create(
            assemblyName: "NavAnalyzerGeneratorTestAssembly",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
            references: [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location)
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    protected static GeneratorDriver RunGenerator(IIncrementalGenerator generator, CSharpCompilation compilation) {
        var driver = CSharpGeneratorDriver.Create(generator.AsSourceGenerator());
        return driver.RunGenerators(compilation);
    }

    protected static string GetGeneratedFile(GeneratorDriver driver, string hintName) {

        var runResult = driver.GetRunResult();
        var generated = runResult.Results[0].GeneratedSources.FirstOrDefault(s => s.HintName == hintName);

        // `default(GeneratedSourceResult)` aus FirstOrDefault hat trotz non-null-Annotation ein null-SourceText.
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (generated.SourceText is null) {
            var available = string.Join(", ", runResult.Results[0].GeneratedSources.Select(s => s.HintName));
            throw new InvalidOperationException($"Generierte Datei '{hintName}' nicht gefunden. Verfügbar: [{available}]");
        }

        return generated.SourceText.ToString();
    }

}
