#region Using Directives

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Basisklasse aller Knoten-Symbole (siehe <see cref="INodeSymbol"/>): hält die typisierte
/// Deklarations-Syntax, den umgebenden Task und die veränderliche Referenzliste, die der
/// <see cref="TaskDefinitionSymbolBuilder"/> beim Verdrahten des Transitionsblocks befüllt.
/// </summary>
/// <typeparam name="T">Der konkrete Typ der Knoten-Deklarations-Syntax.</typeparam>
abstract class NodeSymbol<T>: Symbol, INodeSymbol where T : NodeDeclarationSyntax {

    /// <summary>Initialisiert den Knoten mit Name, Fundstelle, Deklarations-Syntax und umgebendem Task.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="syntax"/> oder
    /// <paramref name="containingTask"/> ist <c>null</c>.</exception>
    protected NodeSymbol(string name, Location location, T syntax, TaskDefinitionSymbol containingTask): base(name, location) {
        Syntax         = syntax         ?? throw new ArgumentNullException(nameof(syntax));
        ContainingTask = containingTask ?? throw new ArgumentNullException(nameof(containingTask));
        References     = new List<INodeReferenceSymbol>();
    }

    /// <inheritdoc/>
    /// <remarks>Stets der Syntaxbaum der Deklaration — für Knoten-Symbole nie <c>null</c>.</remarks>
    public override SyntaxTree SyntaxTree => Syntax.SyntaxTree;

    /// <inheritdoc cref="INodeSymbol.Syntax"/>
    public T Syntax { get; }

    /// <inheritdoc/>
    public ITaskDefinitionSymbol ContainingTask { get; }

    /// <summary>
    /// Veränderliche Sicht auf <see cref="INodeSymbol.References"/> — wird vom
    /// <see cref="TaskDefinitionSymbolBuilder"/> beim Verdrahten der Transitionen befüllt.
    /// </summary>
    public List<INodeReferenceSymbol> References { get; }

    IReadOnlyList<INodeReferenceSymbol> INodeSymbol.References => References;
    NodeDeclarationSyntax INodeSymbol.              Syntax     => Syntax;

    /// <inheritdoc/>
    public virtual IEnumerable<ISymbol> SymbolsAndSelf() {
        yield return this;
    }

    /// <inheritdoc/>
    public abstract bool IsReachable();

}

/// <summary>
/// Basisklasse der reinen Zielknoten (<c>exit</c>, <c>end</c>): nur eingehende Kanten; erreichbar,
/// wenn mindestens eine davon erreichbar ist (<see cref="EdgeExtensions.IsReachable(IEdge)"/>).
/// </summary>
/// <typeparam name="TSyntax">Der konkrete Typ der Knoten-Deklarations-Syntax.</typeparam>
/// <typeparam name="TIncomings">Der Kanten-Typ der eingehenden Kanten.</typeparam>
abstract class NodeSymbolWithOnlyIncomings<TSyntax, TIncomings>: NodeSymbol<TSyntax>
    where TSyntax : NodeDeclarationSyntax
    where TIncomings : IEdge {

    protected NodeSymbolWithOnlyIncomings(string name, Location location, TSyntax syntax, TaskDefinitionSymbol containingTask)
        : base(name, location, syntax, containingTask) {
        Incomings = new List<TIncomings>();
    }

    /// <summary>Veränderliche Sicht auf die eingehenden Kanten (siehe <see cref="ITargetNodeSymbol.Incomings"/>).</summary>
    public List<TIncomings> Incomings { get; }
    /// <inheritdoc/>
    public override bool IsReachable() => Incomings.Any(e => e.IsReachable());

}

/// <summary>
/// Basisklasse der reinen Quellknoten (<c>init</c>): nur ausgehende Kanten; als Anfang des
/// Workflows per Definition stets erreichbar.
/// </summary>
/// <typeparam name="TSyntax">Der konkrete Typ der Knoten-Deklarations-Syntax.</typeparam>
/// <typeparam name="TOutgoings">Der Kanten-Typ der ausgehenden Kanten.</typeparam>
abstract class NodeSymbolWithOnlyOutgoings<TSyntax, TOutgoings>: NodeSymbol<TSyntax>
    where TSyntax : NodeDeclarationSyntax
    where TOutgoings : IEdge {

    protected NodeSymbolWithOnlyOutgoings(string name, Location location, TSyntax syntax, TaskDefinitionSymbol containingTask)
        : base(name, location, syntax, containingTask) {
        Outgoings = new List<TOutgoings>();
    }

    /// <summary>Veränderliche Sicht auf die ausgehenden Kanten (siehe <see cref="ISourceNodeSymbol.Outgoings"/>).</summary>
    public List<TOutgoings> Outgoings { get; }
    /// <inheritdoc/>
    public override bool IsReachable() => true;

}

/// <summary>
/// Basisklasse der Knoten, die Quelle und Ziel von Kanten sein können (<c>choice</c>,
/// <c>dialog</c>, <c>view</c>, <c>task</c>); erreichbar, wenn mindestens eine eingehende Kante
/// erreichbar ist (<see cref="EdgeExtensions.IsReachable(IEdge)"/>).
/// </summary>
/// <typeparam name="TSyntax">Der konkrete Typ der Knoten-Deklarations-Syntax.</typeparam>
/// <typeparam name="TIncomings">Der Kanten-Typ der eingehenden Kanten.</typeparam>
/// <typeparam name="TOutgoings">Der Kanten-Typ der ausgehenden Kanten.</typeparam>
abstract class NodeSymbolWithIncomingsAndOutgoings<TSyntax, TIncomings, TOutgoings>: NodeSymbol<TSyntax>
    where TSyntax : NodeDeclarationSyntax
    where TIncomings : IEdge
    where TOutgoings : IEdge {

    protected NodeSymbolWithIncomingsAndOutgoings(string name, Location location, TSyntax syntax, TaskDefinitionSymbol containingTask)
        : base(name, location, syntax, containingTask) {
        Incomings = new List<TIncomings>();
        Outgoings = new List<TOutgoings>();
    }

    /// <summary>Veränderliche Sicht auf die eingehenden Kanten (siehe <see cref="ITargetNodeSymbol.Incomings"/>).</summary>
    public List<TIncomings> Incomings { get; }
    /// <summary>Veränderliche Sicht auf die ausgehenden Kanten (siehe <see cref="ISourceNodeSymbol.Outgoings"/>).</summary>
    public List<TOutgoings> Outgoings { get; }

    /// <inheritdoc/>
    public override bool IsReachable() => Incomings.Any(e => e.IsReachable());

}

/// <summary>
/// Implementierung von <see cref="IInitNodeSymbol"/>. Der Basis-Name ist stets
/// <see cref="SyntaxFacts.InitKeywordAlt"/> (<c>Init</c>); ein vergebener Alias übersteuert
/// <see cref="Name"/>. Der Konstruktor setzt die Rückreferenz
/// <see cref="InitNodeAliasSymbol.InitNode"/> auf diesen Knoten.
/// </summary>
sealed partial class InitNodeSymbol: NodeSymbolWithOnlyOutgoings<InitNodeDeclarationSyntax, IInitTransition>,
                                     IInitNodeSymbol {

    public InitNodeSymbol(string name, Location location, InitNodeDeclarationSyntax syntax, InitNodeAliasSymbol? alias, TaskDefinitionSymbol containingTask)
        : base(name, location, syntax, containingTask) {

        if (alias != null) {
            alias.InitNode = this;
            Alias          = alias;
        }
    }

    /// <inheritdoc/>
    public IInitNodeAliasSymbol? Alias { get; }

    /// <inheritdoc/>
    /// <remarks>Der Alias-Name, falls vergeben, sonst der Basis-Name <c>Init</c>.</remarks>
    public override string Name => Alias?.Name ?? base.Name;

    IReadOnlyList<IInitTransition> IInitNodeSymbol.Outgoings => Outgoings;
    IReadOnlyList<IEdge> ISourceNodeSymbol.        Outgoings => Outgoings;

    /// <inheritdoc/>
    /// <remarks>Liefert den Knoten selbst und — falls vorhanden — seinen <see cref="Alias"/>.</remarks>
    public override IEnumerable<ISymbol> SymbolsAndSelf() {
        yield return this;

        if (Alias != null) {
            yield return Alias;
        }
    }

}

// Gemeinsame Schnittstelle zur Konstruktion aller Nodes, die Ziel einer Edge sein können
/// <summary>
/// Konstruktions-Sicht der Zielknoten: gibt dem <see cref="TaskDefinitionSymbolBuilder"/> die
/// veränderlichen Listen <see cref="Incomings"/> und <see cref="References"/> zum Verdrahten frei.
/// </summary>
internal interface ITargetNodeSymbolConstruction: ITargetNodeSymbol {

    new List<IEdge>                Incomings  { get; }
    new List<INodeReferenceSymbol> References { get; }

}

/// <summary>Implementierung von <see cref="IExitNodeSymbol"/> — reiner Zielknoten.</summary>
sealed partial class ExitNodeSymbol: NodeSymbolWithOnlyIncomings<ExitNodeDeclarationSyntax, IEdge>,
                                     IExitNodeSymbol, ITargetNodeSymbolConstruction {

    public ExitNodeSymbol(string name, Location location, ExitNodeDeclarationSyntax syntax, TaskDefinitionSymbol containingTask)
        : base(name, location, syntax, containingTask) {
    }

    IReadOnlyList<IEdge> ITargetNodeSymbol.Incomings => Incomings;

}

/// <summary>Implementierung von <see cref="IEndNodeSymbol"/> — reiner Zielknoten.</summary>
sealed partial class EndNodeSymbol: NodeSymbolWithOnlyIncomings<EndNodeDeclarationSyntax, IEdge>,
                                    IEndNodeSymbol, ITargetNodeSymbolConstruction {

    public EndNodeSymbol(string name, Location location, EndNodeDeclarationSyntax syntax, TaskDefinitionSymbol containingTask)
        : base(name, location, syntax, containingTask) {
    }

    IReadOnlyList<IEdge> ITargetNodeSymbol.Incomings => Incomings;

}

/// <summary>
/// Implementierung von <see cref="ITaskNodeSymbol"/>. Ein vergebener Alias übersteuert
/// <see cref="Name"/>; der Konstruktor setzt die Rückreferenz
/// <see cref="TaskNodeAliasSymbol.TaskNode"/> auf diesen Knoten.
/// </summary>
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

    /// <inheritdoc/>
    /// <remarks>Der Alias-Name, falls vergeben, sonst der Name des referenzierten Tasks.</remarks>
    public override string Name => Alias?.Name ?? base.Name;

    /// <inheritdoc cref="ITaskNodeSymbol.Declaration"/>
    public TaskDeclarationSymbol? Declaration { get; }

    ITaskDeclarationSymbol? ITaskNodeSymbol.Declaration => Declaration;

    /// <inheritdoc/>
    public ITaskNodeAliasSymbol? Alias { get; }

    IReadOnlyList<IEdge> ITargetNodeSymbol.        Incomings => Incomings;
    IReadOnlyList<IEdge> ISourceNodeSymbol.        Outgoings => Outgoings;
    IReadOnlyList<IExitTransition> ITaskNodeSymbol.Outgoings => Outgoings;

    /// <inheritdoc/>
    /// <remarks>Liefert den Knoten selbst und — falls vorhanden — seinen <see cref="Alias"/>.</remarks>
    public override IEnumerable<ISymbol> SymbolsAndSelf() {
        yield return this;

        if (Alias != null) {
            yield return Alias;
        }
    }

}

// Gemeinsame Schnittstelle zur Konstruktion von Dialog- und ViewNodes
/// <summary>
/// Konstruktions-Sicht der GUI-Knoten: gibt dem <see cref="TaskDefinitionSymbolBuilder"/> die
/// veränderlichen Listen <see cref="Outgoings"/> und <see cref="References"/> zum Verdrahten frei.
/// </summary>
internal interface IGuiNodeSymbolConstruction: IGuiNodeSymbol {

    new List<ITriggerTransition>   Outgoings  { get; }
    new List<INodeReferenceSymbol> References { get; }

}

/// <summary>Implementierung von <see cref="IDialogNodeSymbol"/> — GUI-Knoten mit Trigger-Transitionen als ausgehenden Kanten.</summary>
sealed partial class DialogNodeSymbol: NodeSymbolWithIncomingsAndOutgoings<DialogNodeDeclarationSyntax, IEdge, ITriggerTransition>,
                                       IDialogNodeSymbol, IGuiNodeSymbolConstruction, ITargetNodeSymbolConstruction {

    public DialogNodeSymbol(string name, Location location, DialogNodeDeclarationSyntax syntax, TaskDefinitionSymbol containingTask)
        : base(name, location, syntax, containingTask) {
    }

    IReadOnlyList<IEdge> ITargetNodeSymbol.          Incomings => Incomings;
    IReadOnlyList<IEdge> ISourceNodeSymbol.          Outgoings => Outgoings;
    IReadOnlyList<ITriggerTransition> IGuiNodeSymbol.Outgoings => Outgoings;

}

/// <summary>Implementierung von <see cref="IViewNodeSymbol"/> — GUI-Knoten mit Trigger-Transitionen als ausgehenden Kanten.</summary>
sealed partial class ViewNodeSymbol: NodeSymbolWithIncomingsAndOutgoings<ViewNodeDeclarationSyntax, IEdge, ITriggerTransition>,
                                     IViewNodeSymbol, IGuiNodeSymbolConstruction, ITargetNodeSymbolConstruction {

    public ViewNodeSymbol(string name, Location location, ViewNodeDeclarationSyntax syntax, TaskDefinitionSymbol containingTask)
        : base(name, location, syntax, containingTask) {
    }

    IReadOnlyList<IEdge> ITargetNodeSymbol.          Incomings => Incomings;
    IReadOnlyList<IEdge> ISourceNodeSymbol.          Outgoings => Outgoings;
    IReadOnlyList<ITriggerTransition> IGuiNodeSymbol.Outgoings => Outgoings;

}

/// <summary>Implementierung von <see cref="IChoiceNodeSymbol"/> — Verzweigungsknoten, Quelle wie Ziel von Kanten.</summary>
sealed partial class ChoiceNodeSymbol: NodeSymbolWithIncomingsAndOutgoings<ChoiceNodeDeclarationSyntax, IEdge, IChoiceTransition>,
                                       IChoiceNodeSymbol, ITargetNodeSymbolConstruction {

    public ChoiceNodeSymbol(string name, Location location, ChoiceNodeDeclarationSyntax syntax, TaskDefinitionSymbol containingTask)
        : base(name, location, syntax, containingTask) {
    }

    IReadOnlyList<IEdge> ITargetNodeSymbol.            Incomings => Incomings;
    IReadOnlyList<IEdge> ISourceNodeSymbol.            Outgoings => Outgoings;
    IReadOnlyList<IChoiceTransition> IChoiceNodeSymbol.Outgoings => Outgoings;

}
