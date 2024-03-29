using System;
using System.Collections.Generic;
using JetBrains.Annotations;
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

    [NotNull]
    public IReadOnlyList<NodeDeclarationSyntax> NodeDeclarations { get; }

    [NotNull]
    public IEnumerable<ConnectionPointNodeSyntax> ConnectionPoints() {
        return NodeDeclarations.OfType<ConnectionPointNodeSyntax>();
    }

    [NotNull]
    public IEnumerable<InitNodeDeclarationSyntax> InitNodes() {
        return NodeDeclarations.OfType<InitNodeDeclarationSyntax>();
    }

    [NotNull]
    public IEnumerable<ExitNodeDeclarationSyntax> ExitNodes() {
        return NodeDeclarations.OfType<ExitNodeDeclarationSyntax>();
    }

    [NotNull]
    public IEnumerable<EndNodeDeclarationSyntax> EndNodes() {
        return NodeDeclarations.OfType<EndNodeDeclarationSyntax>();
    }

    [NotNull]
    public IEnumerable<TaskNodeDeclarationSyntax> TaskNodes() {
        return NodeDeclarations.OfType<TaskNodeDeclarationSyntax>();
    }

    [NotNull]
    public IEnumerable<ChoiceNodeDeclarationSyntax> ChoiceNodes() {
        return NodeDeclarations.OfType<ChoiceNodeDeclarationSyntax>();
    }

    [NotNull]
    public IEnumerable<DialogNodeDeclarationSyntax> DialogNodes() {
        return NodeDeclarations.OfType<DialogNodeDeclarationSyntax>();
    }

    [NotNull]
    public IEnumerable<ViewNodeDeclarationSyntax> ViewNodes() {
        return NodeDeclarations.OfType<ViewNodeDeclarationSyntax>();
    }

    private protected override bool PromiseNoDescendantNodeOfSameType => true;
}