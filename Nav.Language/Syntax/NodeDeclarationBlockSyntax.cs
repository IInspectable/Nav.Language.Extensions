#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

[Serializable]
[SampleSyntax("")]
public partial class NodeDeclarationBlockSyntax : SyntaxNode {


    internal NodeDeclarationBlockSyntax(TextExtent extent, IReadOnlyList<NodeDeclarationSyntax> nodeDeclarations) 
        : base(extent) {

        AddChildNodes(NodeDeclarations = nodeDeclarations);
    }

    public IReadOnlyList<NodeDeclarationSyntax> NodeDeclarations { get; }

    public IEnumerable<ConnectionPointNodeSyntax> ConnectionPoints() {
        return NodeDeclarations.OfType<ConnectionPointNodeSyntax>();
    }

    public IEnumerable<InitNodeDeclarationSyntax> InitNodes() {
        return NodeDeclarations.OfType<InitNodeDeclarationSyntax>();
    }

    public IEnumerable<ExitNodeDeclarationSyntax> ExitNodes() {
        return NodeDeclarations.OfType<ExitNodeDeclarationSyntax>();
    }

    public IEnumerable<EndNodeDeclarationSyntax> EndNodes() {
        return NodeDeclarations.OfType<EndNodeDeclarationSyntax>();
    }

    public IEnumerable<TaskNodeDeclarationSyntax> TaskNodes() {
        return NodeDeclarations.OfType<TaskNodeDeclarationSyntax>();
    }

    public IEnumerable<ChoiceNodeDeclarationSyntax> ChoiceNodes() {
        return NodeDeclarations.OfType<ChoiceNodeDeclarationSyntax>();
    }

    public IEnumerable<DialogNodeDeclarationSyntax> DialogNodes() {
        return NodeDeclarations.OfType<DialogNodeDeclarationSyntax>();
    }

    public IEnumerable<ViewNodeDeclarationSyntax> ViewNodes() {
        return NodeDeclarations.OfType<ViewNodeDeclarationSyntax>();
    }

    private protected override bool PromiseNoDescendantNodeOfSameType => true;
}