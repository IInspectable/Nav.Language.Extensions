#region Using Directives

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace Pharmatechnik.Nav.Language;

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

    public override SyntaxTree SyntaxTree => Syntax.SyntaxTree;

    public TaskDefinitionSyntax          Syntax            { get; }
    public ITaskDeclarationSymbol?       AsTaskDeclaration { get; }
    public SymbolCollection<INodeSymbol> NodeDeclarations  { get; }

    public List<InitTransition>    InitTransitions    { get; }
    public List<ChoiceTransition>  ChoiceTransitions  { get; }
    public List<TriggerTransition> TriggerTransitions { get; }
    public List<ExitTransition>    ExitTransitions    { get; }

    public CodeGenerationUnit? CodeGenerationUnit { get; private set; }

    public string CodeNamespace => (Syntax.SyntaxTree.Root as CodeGenerationUnitSyntax)?.CodeNamespace?.Namespace?.ToString() ?? String.Empty;

    IReadOnlySymbolCollection<INodeSymbol> ITaskDefinitionSymbol.NodeDeclarations   => NodeDeclarations;
    IReadOnlyList<IInitTransition> ITaskDefinitionSymbol.        InitTransitions    => InitTransitions;
    IReadOnlyList<IChoiceTransition> ITaskDefinitionSymbol.      ChoiceTransitions  => ChoiceTransitions;
    IReadOnlyList<ITriggerTransition> ITaskDefinitionSymbol.     TriggerTransitions => TriggerTransitions;
    IReadOnlyList<IExitTransition> ITaskDefinitionSymbol.        ExitTransitions    => ExitTransitions;

    public IEnumerable<ISymbol> SymbolsAndSelf() {

        yield return this;

        foreach (var symbol in NodeDeclarations.SelectMany(nd => nd.SymbolsAndSelf())) {
            yield return symbol;
        }

        foreach (var symbol in Edges().SelectMany(t => t.Symbols())) {
            yield return symbol;
        }
    }

    public IEnumerable<IEdge> Edges() {
        return Enumerable.Empty<IEdge>()
                         .Concat(InitTransitions)
                         .Concat(ChoiceTransitions)
                         .Concat(TriggerTransitions)
                         .Concat(ExitTransitions);
    }

    internal void FinalConstruct(CodeGenerationUnit codeGenerationUnit) {
        CodeGenerationUnit = codeGenerationUnit;
    }

}
