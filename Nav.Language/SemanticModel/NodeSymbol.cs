#region Using Directives

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace Pharmatechnik.Nav.Language;

abstract class NodeSymbol<T>: Symbol, INodeSymbol where T : NodeDeclarationSyntax {

    protected NodeSymbol(string name, Location location, T syntax, TaskDefinitionSymbol containingTask): base(name, location) {
        Syntax         = syntax         ?? throw new ArgumentNullException(nameof(syntax));
        ContainingTask = containingTask ?? throw new ArgumentNullException(nameof(containingTask));
        References     = new List<INodeReferenceSymbol>();
    }

    public override SyntaxTree SyntaxTree => Syntax.SyntaxTree;

    public T Syntax { get; }

    public ITaskDefinitionSymbol ContainingTask { get; }

    public List<INodeReferenceSymbol> References { get; }

    IReadOnlyList<INodeReferenceSymbol> INodeSymbol.References => References;
    NodeDeclarationSyntax INodeSymbol.              Syntax     => Syntax;

    public virtual IEnumerable<ISymbol> SymbolsAndSelf() {
        yield return this;
    }

    public abstract bool IsReachable();

}

abstract class NodeSymbolWithOnlyIncomings<TSyntax, TIncomings>: NodeSymbol<TSyntax>
    where TSyntax : NodeDeclarationSyntax
    where TIncomings : IEdge {

    protected NodeSymbolWithOnlyIncomings(string name, Location location, TSyntax syntax, TaskDefinitionSymbol containingTask)
        : base(name, location, syntax, containingTask) {
        Incomings = new List<TIncomings>();
    }

    public List<TIncomings> Incomings { get; }
    public override bool IsReachable() => Incomings.Any(e => e.IsReachable());

}

abstract class NodeSymbolWithOnlyOutgoings<TSyntax, TOutgoings>: NodeSymbol<TSyntax>
    where TSyntax : NodeDeclarationSyntax
    where TOutgoings : IEdge {

    protected NodeSymbolWithOnlyOutgoings(string name, Location location, TSyntax syntax, TaskDefinitionSymbol containingTask)
        : base(name, location, syntax, containingTask) {
        Outgoings = new List<TOutgoings>();
    }

    public List<TOutgoings> Outgoings { get; }
    public override bool IsReachable() => true;

}

abstract class NodeSymbolWithIncomingsAndOutgoings<TSyntax, TIncomings, TOutgoings>: NodeSymbol<TSyntax>
    where TSyntax : NodeDeclarationSyntax
    where TIncomings : IEdge
    where TOutgoings : IEdge {

    protected NodeSymbolWithIncomingsAndOutgoings(string name, Location location, TSyntax syntax, TaskDefinitionSymbol containingTask)
        : base(name, location, syntax, containingTask) {
        Incomings = new List<TIncomings>();
        Outgoings = new List<TOutgoings>();
    }

    public List<TIncomings> Incomings { get; }
    public List<TOutgoings> Outgoings { get; }

    public override bool IsReachable() => Incomings.Any(e => e.IsReachable());

}

sealed partial class InitNodeSymbol: NodeSymbolWithOnlyOutgoings<InitNodeDeclarationSyntax, IInitTransition>,
                                     IInitNodeSymbol {

    public InitNodeSymbol(string name, Location location, InitNodeDeclarationSyntax syntax, InitNodeAliasSymbol? alias, TaskDefinitionSymbol containingTask)
        : base(name, location, syntax, containingTask) {

        if (alias != null) {
            alias.InitNode = this;
            Alias          = alias;
        }
    }

    public IInitNodeAliasSymbol? Alias { get; }

    public override string Name => Alias?.Name ?? base.Name;

    IReadOnlyList<IInitTransition> IInitNodeSymbol.Outgoings => Outgoings;
    IReadOnlyList<IEdge> ISourceNodeSymbol.        Outgoings => Outgoings;

    public override IEnumerable<ISymbol> SymbolsAndSelf() {
        yield return this;

        if (Alias != null) {
            yield return Alias;
        }
    }

}

// Gemeinsame Schnittstelle zur Konstruktion aller Nodes, die Ziel einer Edge sein können
internal interface ITargetNodeSymbolConstruction: ITargetNodeSymbol {

    new List<IEdge>                Incomings  { get; }
    new List<INodeReferenceSymbol> References { get; }

}

sealed partial class ExitNodeSymbol: NodeSymbolWithOnlyIncomings<ExitNodeDeclarationSyntax, IEdge>,
                                     IExitNodeSymbol, ITargetNodeSymbolConstruction {

    public ExitNodeSymbol(string name, Location location, ExitNodeDeclarationSyntax syntax, TaskDefinitionSymbol containingTask)
        : base(name, location, syntax, containingTask) {
    }

    IReadOnlyList<IEdge> ITargetNodeSymbol.Incomings => Incomings;

}

sealed partial class EndNodeSymbol: NodeSymbolWithOnlyIncomings<EndNodeDeclarationSyntax, IEdge>,
                                    IEndNodeSymbol, ITargetNodeSymbolConstruction {

    public EndNodeSymbol(string name, Location location, EndNodeDeclarationSyntax syntax, TaskDefinitionSymbol containingTask)
        : base(name, location, syntax, containingTask) {
    }

    IReadOnlyList<IEdge> ITargetNodeSymbol.Incomings => Incomings;

}

sealed partial class TaskNodeSymbol: NodeSymbolWithIncomingsAndOutgoings<TaskNodeDeclarationSyntax, IEdge, IExitTransition>,
                                     ITaskNodeSymbol, ITargetNodeSymbolConstruction {

    public TaskNodeSymbol(string name, Location location, TaskNodeDeclarationSyntax syntax, TaskNodeAliasSymbol? alias,
                          TaskDeclarationSymbol? declaration, TaskDefinitionSymbol containingTask)
        : base(name, location, syntax, containingTask) {
        Declaration = declaration;

        if (alias != null) {
            alias.TaskNode = this;
            Alias          = alias;
        }
    }

    public override string Name => Alias?.Name ?? base.Name;

    public TaskDeclarationSymbol? Declaration { get; }

    ITaskDeclarationSymbol? ITaskNodeSymbol.Declaration => Declaration;

    public ITaskNodeAliasSymbol? Alias { get; }

    IReadOnlyList<IEdge> ITargetNodeSymbol.        Incomings => Incomings;
    IReadOnlyList<IEdge> ISourceNodeSymbol.        Outgoings => Outgoings;
    IReadOnlyList<IExitTransition> ITaskNodeSymbol.Outgoings => Outgoings;

    public override IEnumerable<ISymbol> SymbolsAndSelf() {
        yield return this;

        if (Alias != null) {
            yield return Alias;
        }
    }

}

// Gemeinsame Schnittstelle zur Konstruktion von Dialog- und ViewNodes
internal interface IGuiNodeSymbolConstruction: IGuiNodeSymbol {

    new List<ITriggerTransition>   Outgoings  { get; }
    new List<INodeReferenceSymbol> References { get; }

}

sealed partial class DialogNodeSymbol: NodeSymbolWithIncomingsAndOutgoings<DialogNodeDeclarationSyntax, IEdge, ITriggerTransition>,
                                       IDialogNodeSymbol, IGuiNodeSymbolConstruction, ITargetNodeSymbolConstruction {

    public DialogNodeSymbol(string name, Location location, DialogNodeDeclarationSyntax syntax, TaskDefinitionSymbol containingTask)
        : base(name, location, syntax, containingTask) {
    }

    IReadOnlyList<IEdge> ITargetNodeSymbol.          Incomings => Incomings;
    IReadOnlyList<IEdge> ISourceNodeSymbol.          Outgoings => Outgoings;
    IReadOnlyList<ITriggerTransition> IGuiNodeSymbol.Outgoings => Outgoings;

}

sealed partial class ViewNodeSymbol: NodeSymbolWithIncomingsAndOutgoings<ViewNodeDeclarationSyntax, IEdge, ITriggerTransition>,
                                     IViewNodeSymbol, IGuiNodeSymbolConstruction, ITargetNodeSymbolConstruction {

    public ViewNodeSymbol(string name, Location location, ViewNodeDeclarationSyntax syntax, TaskDefinitionSymbol containingTask)
        : base(name, location, syntax, containingTask) {
    }

    IReadOnlyList<IEdge> ITargetNodeSymbol.          Incomings => Incomings;
    IReadOnlyList<IEdge> ISourceNodeSymbol.          Outgoings => Outgoings;
    IReadOnlyList<ITriggerTransition> IGuiNodeSymbol.Outgoings => Outgoings;

}

sealed partial class ChoiceNodeSymbol: NodeSymbolWithIncomingsAndOutgoings<ChoiceNodeDeclarationSyntax, IEdge, IChoiceTransition>,
                                       IChoiceNodeSymbol, ITargetNodeSymbolConstruction {

    public ChoiceNodeSymbol(string name, Location location, ChoiceNodeDeclarationSyntax syntax, TaskDefinitionSymbol containingTask)
        : base(name, location, syntax, containingTask) {
    }

    IReadOnlyList<IEdge> ITargetNodeSymbol.            Incomings => Incomings;
    IReadOnlyList<IEdge> ISourceNodeSymbol.            Outgoings => Outgoings;
    IReadOnlyList<IChoiceTransition> IChoiceNodeSymbol.Outgoings => Outgoings;

}
