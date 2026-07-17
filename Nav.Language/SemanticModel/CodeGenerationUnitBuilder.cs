#region Using Directives

using System;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using System.Collections.Immutable;

using Pharmatechnik.Nav.Language.SemanticAnalyzer;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Baut aus einer <see cref="CodeGenerationUnitSyntax"/> das semantische Modell der Datei
/// (<see cref="CodeGenerationUnit"/>): orchestriert den <see cref="TaskDeclarationSymbolBuilder"/>
/// (Deklarationstabelle + Includes) und den <see cref="TaskDefinitionSymbolBuilder"/> (je
/// <c>task</c>-Definition), sammelt die <c>[using …]</c>-Namespaces und den Symbol-Strom ein und
/// lässt abschließend die semantischen Analyzer über das Modell laufen. Einstiegspunkt ist
/// <see cref="FromCodeGenerationUnitSyntax"/> (Vordertür:
/// <see cref="CodeGenerationUnit.FromCodeGenerationUnitSyntax"/>).
/// </summary>
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

    /// <summary>
    /// Baut das Modell in zwei Phasen: Zuerst entsteht ein temporäres Modell mit den Diagnostics
    /// des Modellbaus, auf das die Task-Definitionen rückverdrahtet werden
    /// (<see cref="TaskDefinitionSymbol.FinalConstruct"/>), damit die semantischen Analyzer ein
    /// vollständig verdrahtetes Modell sehen. Deren Ergebnisse ergeben zusammen mit den
    /// Modellbau-Diagnostics das finale Modell
    /// (<see cref="CodeGenerationUnit.WithDiagnostics"/>), auf das Definitionen und die nicht
    /// inkludierten Deklarationen erneut rückverdrahtet werden — inkludierte Deklarationen
    /// behalten bewusst keine Modell-Referenz
    /// (<see cref="ITaskDeclarationSymbol.CodeGenerationUnit"/> bleibt <c>null</c>).
    /// </summary>
    /// <param name="syntax">Die Wurzel-Syntax der Datei.</param>
    /// <param name="cancellationToken">Zum Abbrechen des Vorgangs.</param>
    /// <param name="syntaxProvider">Liefert die Syntax inkludierter Dateien; <c>null</c> fällt auf
    /// <see cref="SyntaxProvider.Default"/> zurück.</param>
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

    /// <summary>
    /// Verarbeitet den Nav-Anteil der Datei in fester Reihenfolge: erst die Deklarationstabelle
    /// samt Includes (<see cref="TaskDeclarationSymbolBuilder"/>), dann die
    /// <c>task</c>-Definitionen (<see cref="ProcessTaskDefinitionSyntax"/>), zuletzt der
    /// Symbol-Strom für <see cref="CodeGenerationUnit.Symbols"/> — dieser enthält nur Symbole der
    /// eigenen Datei: die <c>taskref Name { … }</c>-Deklarationen
    /// (<see cref="TaskDeclarationOrigin.TaskDeclaration"/>, nicht inkludiert), alle Symbole der
    /// Task-Definitionen samt Kindern sowie die Includes selbst.
    /// </summary>
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

    /// <summary>
    /// Baut zu einer <c>task</c>-Definition das <see cref="TaskDefinitionSymbol"/>
    /// (<see cref="TaskDefinitionSymbolBuilder"/>). Definitionen mit bereits vergebenem Namen
    /// werden übersprungen — der Namenskonflikt ist über die Deklarationstabelle schon als
    /// Nav0020 gemeldet.
    /// </summary>
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

    /// <summary>
    /// Sammelt die Namespaces der <c>[using …]</c>-Deklarationen für
    /// <see cref="CodeGenerationUnit.CodeUsings"/> ein; Deklarationen ohne Namespace-Angabe
    /// werden ausgelassen.
    /// </summary>
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
