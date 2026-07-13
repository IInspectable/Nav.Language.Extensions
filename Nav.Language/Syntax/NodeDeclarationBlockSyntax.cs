using System;
using System.Collections.Generic;
using System.Linq;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

/// <summary>
/// Der Knoten-Deklarationsblock einer <c>task</c>-Definition
/// (<see cref="TaskDefinitionSyntax.NodeDeclarationBlock"/>): die Folge aller Knoten-Deklarationen am
/// Anfang des <c>{ … }</c>-Rumpfs, vor dem Transitionsblock — z.B. <c>init I1;</c>,
/// <c>view AuswahlDialog;</c>, <c>exit Fertig;</c>. Ein Block ohne einen einzigen Knoten erhält den
/// Extent <see cref="TextExtent.Missing"/>.
/// </summary>
[Serializable]
[SampleSyntax("")]
public partial class NodeDeclarationBlockSyntax : SyntaxNode {


    internal NodeDeclarationBlockSyntax(TextExtent extent, IReadOnlyList<NodeDeclarationSyntax> nodeDeclarations) 
        : base(extent) {

        AddChildNodes(NodeDeclarations = nodeDeclarations);
    }

    /// <summary>Alle Knoten-Deklarationen des Blocks in Quelltext-Reihenfolge.</summary>
    public IReadOnlyList<NodeDeclarationSyntax> NodeDeclarations { get; }

    /// <summary>
    /// Die Verbindungspunkt-Deklarationen (<c>init</c>/<c>exit</c>/<c>end</c>) des Blocks — die von
    /// außen sichtbare Schnittstelle des Tasks (siehe <see cref="ConnectionPointNodeSyntax"/>).
    /// </summary>
    public IEnumerable<ConnectionPointNodeSyntax> ConnectionPoints() {
        return NodeDeclarations.OfType<ConnectionPointNodeSyntax>();
    }

    /// <summary>Die <c>init</c>-Knoten-Deklarationen des Blocks.</summary>
    public IEnumerable<InitNodeDeclarationSyntax> InitNodes() {
        return NodeDeclarations.OfType<InitNodeDeclarationSyntax>();
    }

    /// <summary>Die <c>exit</c>-Knoten-Deklarationen des Blocks.</summary>
    public IEnumerable<ExitNodeDeclarationSyntax> ExitNodes() {
        return NodeDeclarations.OfType<ExitNodeDeclarationSyntax>();
    }

    /// <summary>Die <c>end</c>-Knoten-Deklarationen des Blocks.</summary>
    public IEnumerable<EndNodeDeclarationSyntax> EndNodes() {
        return NodeDeclarations.OfType<EndNodeDeclarationSyntax>();
    }

    /// <summary>Die <c>task</c>-Knoten-Deklarationen des Blocks.</summary>
    public IEnumerable<TaskNodeDeclarationSyntax> TaskNodes() {
        return NodeDeclarations.OfType<TaskNodeDeclarationSyntax>();
    }

    /// <summary>Die <c>choice</c>-Knoten-Deklarationen des Blocks.</summary>
    public IEnumerable<ChoiceNodeDeclarationSyntax> ChoiceNodes() {
        return NodeDeclarations.OfType<ChoiceNodeDeclarationSyntax>();
    }

    /// <summary>Die <c>dialog</c>-Knoten-Deklarationen des Blocks.</summary>
    public IEnumerable<DialogNodeDeclarationSyntax> DialogNodes() {
        return NodeDeclarations.OfType<DialogNodeDeclarationSyntax>();
    }

    /// <summary>Die <c>view</c>-Knoten-Deklarationen des Blocks.</summary>
    public IEnumerable<ViewNodeDeclarationSyntax> ViewNodes() {
        return NodeDeclarations.OfType<ViewNodeDeclarationSyntax>();
    }

    /// <summary>
    /// <c>true</c> — ein Knoten-Deklarationsblock enthält nie einen weiteren Knoten-Deklarationsblock;
    /// die typisierte Nachfahren-Suche kann den Abstieg daher früh abbrechen (siehe Basisimplementierung
    /// in <see cref="SyntaxNode"/>).
    /// </summary>
    private protected override bool PromiseNoDescendantNodeOfSameType => true;
}