using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Der Fortsetzungs-Anhang einer Transition (ab Sprachversion 2): <c>… o-^ Task</c> bzw. <c>… --^ Task</c>.
/// Er hängt hinter dem Zielknoten (dem tragenden GUI-Knoten) einer <see cref="TransitionDefinitionSyntax"/>
/// bzw. <see cref="ExitTransitionDefinitionSyntax"/> und benennt die Fortsetzungs-Kante samt Ziel-Task.
/// <see cref="Edge"/> und <see cref="TargetNode"/> sind — wie bei der umgebenden Transition — fehlertolerant
/// optional.
/// </summary>
[Serializable]
[SampleSyntax("o-^ Task")]
public partial class ContinuationTransitionSyntax: SyntaxNode {

    internal ContinuationTransitionSyntax(TextExtent extent,
                                          ContinuationEdgeSyntax? edge,
                                          TargetNodeSyntax? targetNode): base(extent) {

        AddChildNode(Edge       = edge);
        AddChildNode(TargetNode = targetNode);
    }

    public ContinuationEdgeSyntax? Edge { get; }

    public TargetNodeSyntax? TargetNode { get; }

}
