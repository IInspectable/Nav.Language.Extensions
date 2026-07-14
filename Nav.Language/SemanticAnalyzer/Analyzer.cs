#region Using Directives

using System;
using System.Linq;
using System.Threading;
using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer;

public interface INavAnalyzer {

    IEnumerable<Diagnostic> Analyze(CodeGenerationUnit codeGenerationUnit, AnalyzerContext context);

}

public abstract class NavAnalyzer: INavAnalyzer {

    public abstract DiagnosticDescriptor Descriptor { get; }

    public virtual IEnumerable<Diagnostic> Analyze(CodeGenerationUnit codeGenerationUnit, AnalyzerContext context) {
            
        foreach (var diag in codeGenerationUnit.TaskDeclarations.SelectMany(taskDeclaration=> Analyze(taskDeclaration, context))) {
            yield return diag;
        }

        foreach (var diag in codeGenerationUnit.TaskDefinitions.SelectMany(taskDefinition=> Analyze(taskDefinition, context))) {
            yield return diag;
        }
    }

    public virtual IEnumerable<Diagnostic> Analyze(ITaskDeclarationSymbol taskDeclaration, AnalyzerContext context) {
        yield break;
    }

    public virtual IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        yield break;
    }

}

static partial class Analyzer {

    // Die Analyzer-Liste wird vom Nav.Analyzer.SourceGenerator als CreateAll() erzeugt (statische Verweise
    // auf alle INavAnalyzer-Implementierungen). Der statische Verweis ersetzt die frühere Reflection
    // (Assembly.ExportedTypes + Activator.CreateInstance) und ist trim-sicher.
    private static readonly Lazy<IList<INavAnalyzer>> AnalyzerList = new(
        () => CreateAll(),
        LazyThreadSafetyMode.PublicationOnly);

    public static IEnumerable<INavAnalyzer> GetAnalyzer() {
        return AnalyzerList.Value;
    }

}