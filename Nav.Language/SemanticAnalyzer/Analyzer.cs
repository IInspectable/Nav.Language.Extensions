#region Using Directives

using System;
using System.Linq;
using System.Threading;
using System.Reflection;
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

static class Analyzer {

    private static readonly Lazy<IList<INavAnalyzer>> TaskDefinitionAnalyzer = new(
        () => GetInterfaceImplementationsFromAssembly<INavAnalyzer>().ToList(),
        LazyThreadSafetyMode.PublicationOnly);

    public static IEnumerable<INavAnalyzer> GetAnalyzer() {
        return TaskDefinitionAnalyzer.Value;
    }

    private static IEnumerable<T> GetInterfaceImplementationsFromAssembly<T>() where T : class {

        var dll   = typeof(Analyzer).GetTypeInfo().Assembly;
        var rules = new List<T>();

        foreach (var type in dll.ExportedTypes) {
            var typeInfo = type.GetTypeInfo();
            if (!typeInfo.IsInterface
             && !typeInfo.IsAbstract
             && typeInfo.ImplementedInterfaces.Contains(typeof(T))) {

                var ruleObj = Activator.CreateInstance(type);
                if (!(ruleObj is T rule)) {

                    continue;
                }

                rules.Add(rule);
            }
        }

        return rules;
    }

}