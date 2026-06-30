using System;
using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Pharmatechnik.Nav.Language.Grammar.SourceGenerator.Tests.Shared;

/// <summary>
/// Basis für die Generator-Tests: baut eine In-Memory-Compilation aus Quelltext und lässt den
/// <see cref="NavGrammarGenerator"/> darüber laufen. Der Generator arbeitet rein syntaktisch — eine
/// minimale Referenzliste genügt, und semantische Fehler der Compilation stören nicht.
/// </summary>
public abstract class GeneratorTestBase {

    protected static CSharpCompilation CreateCompilation(string source) {
        return CSharpCompilation.Create(
            assemblyName: "NavGrammarGeneratorTestAssembly",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
            references: [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location)
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    protected static GeneratorDriver RunGenerator(CSharpCompilation compilation) {
        var driver = CSharpGeneratorDriver.Create(new NavGrammarGenerator().AsSourceGenerator());
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

    protected static ImmutableArray<Diagnostic> GetGeneratorDiagnostics(GeneratorDriver driver) {
        return driver.GetRunResult().Results[0].Diagnostics;
    }

}
