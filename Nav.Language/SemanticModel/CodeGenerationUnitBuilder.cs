#nullable enable

#region Using Directives

using System;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using System.Collections.Immutable;

using Pharmatechnik.Nav.Language.SemanticAnalyzer;

#endregion

namespace Pharmatechnik.Nav.Language;

sealed class CodeGenerationUnitBuilder {

    readonly ISyntaxProvider                         _syntaxProvider;
    readonly ImmutableArray<Diagnostic>.Builder      _diagnostics;
    readonly SymbolCollection<TaskDeclarationSymbol> _taskDeclarations;
    readonly SymbolCollection<TaskDefinitionSymbol>  _taskDefinitions;
    readonly SymbolCollection<IncludeSymbol>         _includes;
    readonly ImmutableArray<string>.Builder          _codeUsings;
    readonly List<ISymbol>                           _symbols;

    CodeGenerationUnitBuilder(ISyntaxProvider? syntaxProvider) {
        _syntaxProvider   = syntaxProvider ?? SyntaxProvider.Default;
        _diagnostics      = ImmutableArray.CreateBuilder<Diagnostic>();
        _taskDeclarations = new SymbolCollection<TaskDeclarationSymbol>();
        _taskDefinitions  = new SymbolCollection<TaskDefinitionSymbol>();
        _includes         = new SymbolCollection<IncludeSymbol>();
        _codeUsings       = ImmutableArray.CreateBuilder<string>();
        _symbols          = new List<ISymbol>();
    }

    public static CodeGenerationUnit FromCodeGenerationUnitSyntax(CodeGenerationUnitSyntax syntax, CancellationToken cancellationToken, ISyntaxProvider? syntaxProvider) {

        if (syntax == null) {
            throw new ArgumentNullException(nameof(syntax));
        }

        var builder = new CodeGenerationUnitBuilder(syntaxProvider);

        builder.Process(syntax, cancellationToken);

        // Temporary model for analyzing
        var tempModel = new CodeGenerationUnit(
            syntax,
            builder._codeUsings.ToImmutable(),
            builder._taskDeclarations,
            builder._taskDefinitions,
            builder._includes,
            builder._symbols,
            builder._diagnostics.ToImmutable());

        foreach (var taskDefinition in builder._taskDefinitions) {
            taskDefinition.FinalConstruct(tempModel);
        }

        // Analyze Model
        var analyzers   = Analyzer.GetAnalyzer();
        var context     = new AnalyzerContext();
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        foreach (var analyzer in analyzers) {

            cancellationToken.ThrowIfCancellationRequested();

            diagnostics.AddRange(analyzer.Analyze(tempModel, context));
        }

        // Bisherige Diagnostics anhängen
        diagnostics.AddRange(tempModel.Diagnostics);

        // Finales Model mit allen Diagnostics
        var model = tempModel.WithDiagnostics(diagnostics.ToImmutable());

        foreach (var taskDefinition in builder._taskDefinitions) {
            taskDefinition.FinalConstruct(model);
        }

        foreach (var taskDeclaration in builder._taskDeclarations.Where(td => !td.IsIncluded)) {
            taskDeclaration.FinalConstruct(model);
        }

        return model;
    }

    void Process(CodeGenerationUnitSyntax syntax, CancellationToken cancellationToken) {
        ProcessNavLanguage(syntax, cancellationToken);
        ProcessCodeLanguage(syntax, cancellationToken);
    }

    void ProcessNavLanguage(CodeGenerationUnitSyntax syntax, CancellationToken cancellationToken) {

        cancellationToken.ThrowIfCancellationRequested();

        //====================
        // 1. TaskDeclarations
        //====================
        var taskDeclarationResult = TaskDeclarationSymbolBuilder.FromCodeGenerationUnitSyntax(syntax, _syntaxProvider, cancellationToken);

        _diagnostics.AddRange(taskDeclarationResult.Diagnostics);
        _taskDeclarations.AddRange(taskDeclarationResult.TaskDeclarations);
        _includes.AddRange(taskDeclarationResult.Includes);

        cancellationToken.ThrowIfCancellationRequested();

        //====================
        // 2. TaskDefinitions
        //====================
        foreach (var taskDefinitionSyntax in syntax.DescendantNodes<TaskDefinitionSyntax>()) {
            ProcessTaskDefinitionSyntax(taskDefinitionSyntax, cancellationToken);
        }

        //====================
        // 3. Collect Symbols
        //====================
        // Nur Symbole von Taskdeklarationen der eigenen Datei, und auch nur solche, die aus "taskrefs task" entstanden sind
        _symbols.AddRange(_taskDeclarations.Where(td => !td.IsIncluded &&
                                                        td.Origin == TaskDeclarationOrigin.TaskDeclaration)
                                           .SelectMany(td => td.SymbolsAndSelf()));

        // Alle Symbole und deren "Kinder" der Taskdefinitionen
        _symbols.AddRange(_taskDefinitions.SelectMany(td => td.SymbolsAndSelf()));

        // Alle Includes (= taskref "filepath") hinzufügen
        _symbols.AddRange(_includes);
    }

    void ProcessTaskDefinitionSyntax(TaskDefinitionSyntax taskDefinitionSyntax, CancellationToken cancellationToken) {

        cancellationToken.ThrowIfCancellationRequested();

        var (taskDefinition, diagnostics) = TaskDefinitionSymbolBuilder.Build(taskDefinitionSyntax, _taskDeclarations);
        _diagnostics.AddRange(diagnostics);

        if (taskDefinition == null) {
            return;
        }

        // Doppelte Einträge überspringen. Fehler existiert schon wegen der Taskdeclarations.
        if (!_taskDefinitions.Contains(taskDefinition.Name)) {
            _taskDefinitions.Add(taskDefinition);
        }
    }

    void ProcessCodeLanguage(CodeGenerationUnitSyntax syntax, CancellationToken cancellationToken) {
        foreach (var codeUsingDeclarationSyntax in syntax.DescendantNodes<CodeUsingDeclarationSyntax>()) {

            cancellationToken.ThrowIfCancellationRequested();

            if (codeUsingDeclarationSyntax.Namespace == null) {
                continue;
            }

            var nsSyntax = codeUsingDeclarationSyntax.Namespace;
            var ns       = nsSyntax.ToString();

            _codeUsings.Add(ns);
        }
    }

}
