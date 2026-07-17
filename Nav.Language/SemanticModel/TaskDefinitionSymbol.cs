#region Using Directives

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Die Implementierung von <see cref="ITaskDefinitionSymbol"/>. Erzeugt und verdrahtet wird sie im
/// <see cref="TaskDefinitionSymbolBuilder"/>; Knoten- und Transitions-Kollektionen sind hier
/// schreibbar und werden erst über die Interface-Sicht read-only.
/// </summary>
sealed partial class TaskDefinitionSymbol: Symbol, ITaskDefinitionSymbol {

    public TaskDefinitionSymbol(string name,
                                Location location,
                                TaskDefinitionSyntax syntax,
                                TaskDeclarationSymbol? taskDeclaration): base(name, location) {
        Syntax             = syntax;
        AsTaskDeclaration  = taskDeclaration;
        NodeDeclarations   = new SymbolCollection<INodeSymbol>();
        InitTransitions    = new List<InitTransition>();
        ChoiceTransitions  = new List<ChoiceTransition>();
        TriggerTransitions = new List<TriggerTransition>();
        ExitTransitions    = new List<ExitTransition>();
    }

    /// <summary>
    /// Der Syntaxbaum der definierenden Datei — anders als bei Task-Deklarationen nie <c>null</c>,
    /// da eine Definition stets aus der eigenen Datei stammt.
    /// </summary>
    public override SyntaxTree SyntaxTree => Syntax.SyntaxTree;

    /// <inheritdoc/>
    public TaskDefinitionSyntax          Syntax            { get; }
    /// <inheritdoc/>
    public ITaskDeclarationSymbol?       AsTaskDeclaration { get; }
    /// <summary>Die Knoten in schreibbarer Form — befüllt vom <see cref="TaskDefinitionSymbolBuilder"/>.</summary>
    public SymbolCollection<INodeSymbol> NodeDeclarations  { get; }

    /// <summary>Die Init-Transitionen in schreibbarer Form — befüllt vom <see cref="TaskDefinitionSymbolBuilder"/>.</summary>
    public List<InitTransition>    InitTransitions    { get; }
    /// <summary>Die Choice-Transitionen in schreibbarer Form — befüllt vom <see cref="TaskDefinitionSymbolBuilder"/>.</summary>
    public List<ChoiceTransition>  ChoiceTransitions  { get; }
    /// <summary>Die Trigger-Transitionen in schreibbarer Form — befüllt vom <see cref="TaskDefinitionSymbolBuilder"/>.</summary>
    public List<TriggerTransition> TriggerTransitions { get; }
    /// <summary>Die Exit-Transitionen in schreibbarer Form — befüllt vom <see cref="TaskDefinitionSymbolBuilder"/>.</summary>
    public List<ExitTransition>    ExitTransitions    { get; }

    /// <inheritdoc/>
    public CodeGenerationUnit? CodeGenerationUnit { get; private set; }

    /// <inheritdoc/>
    public string CodeNamespace => (Syntax.SyntaxTree.Root as CodeGenerationUnitSyntax)?.CodeNamespace?.Namespace?.ToString() ?? String.Empty;

    IReadOnlySymbolCollection<INodeSymbol> ITaskDefinitionSymbol.NodeDeclarations   => NodeDeclarations;
    IReadOnlyList<IInitTransition> ITaskDefinitionSymbol.        InitTransitions    => InitTransitions;
    IReadOnlyList<IChoiceTransition> ITaskDefinitionSymbol.      ChoiceTransitions  => ChoiceTransitions;
    IReadOnlyList<ITriggerTransition> ITaskDefinitionSymbol.     TriggerTransitions => TriggerTransitions;
    IReadOnlyList<IExitTransition> ITaskDefinitionSymbol.        ExitTransitions    => ExitTransitions;

    /// <summary>
    /// Liefert diese Definition samt aller enthaltenen Symbole: die Knoten mit ihren
    /// Kind-Symbolen (<see cref="INodeSymbol.SymbolsAndSelf"/>) sowie die Symbol-Bestandteile
    /// aller Kanten (<see cref="IEdge.Symbols"/>). Hierüber sammelt der
    /// <see cref="CodeGenerationUnitBuilder"/> die Symbole der Datei ein.
    /// </summary>
    public IEnumerable<ISymbol> SymbolsAndSelf() {

        yield return this;

        foreach (var symbol in NodeDeclarations.SelectMany(nd => nd.SymbolsAndSelf())) {
            yield return symbol;
        }

        foreach (var symbol in Edges().SelectMany(t => t.Symbols())) {
            yield return symbol;
        }
    }

    /// <inheritdoc/>
    public IEnumerable<IEdge> Edges() {
        return Enumerable.Empty<IEdge>()
                         .Concat(InitTransitions)
                         .Concat(ChoiceTransitions)
                         .Concat(TriggerTransitions)
                         .Concat(ExitTransitions);
    }

    /// <summary>
    /// Setzt nachträglich die Rückreferenz auf das semantische Modell. Der
    /// <see cref="CodeGenerationUnitBuilder"/> ruft dies zweimal auf: zunächst mit dem temporären
    /// Modell für die semantischen Analyzer, danach mit dem finalen Modell samt aller Diagnostics.
    /// </summary>
    internal void FinalConstruct(CodeGenerationUnit codeGenerationUnit) {
        CodeGenerationUnit = codeGenerationUnit;
    }

}
