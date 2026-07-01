using System;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Pharmatechnik.Nav.Language.Visitor.SourceGenerator.Tests.Shared;

/// <summary>
/// Basis für die Generator-Tests: baut eine In-Memory-Compilation aus Quelltext und lässt einen der
/// Besucher-Generatoren darüber laufen. Anders als der rein syntaktische Grammatik-Generator arbeiten
/// diese Generatoren <b>semantisch</b> (Basistyp-Kette bzw. implementierte Interfaces) — der Test-Quelltext
/// muss daher die Minimalbasen (<c>SyntaxNode</c> bzw. <c>ISymbol</c>) selbst mitbringen.
/// </summary>
public abstract class GeneratorTestBase {

    protected static CSharpCompilation CreateCompilation(string source) {
        return CSharpCompilation.Create(
            assemblyName: "NavVisitorGeneratorTestAssembly",
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

        if (generated.SourceText is null) {
            var available = string.Join(", ", runResult.Results[0].GeneratedSources.Select(s => s.HintName));
            throw new InvalidOperationException($"Generierte Datei '{hintName}' nicht gefunden. Verfügbar: [{available}]");
        }

        return generated.SourceText.ToString();
    }

}
