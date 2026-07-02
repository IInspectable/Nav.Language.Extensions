#nullable enable

using System;
using System.Collections.Generic;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

[Serializable]
[SampleSyntax("")]
public partial class TransitionDefinitionBlockSyntax: SyntaxNode {

    internal TransitionDefinitionBlockSyntax(TextExtent extent,
                                             IReadOnlyList<TransitionDefinitionSyntax> transitionDefinitions,
                                             IReadOnlyList<ExitTransitionDefinitionSyntax> exitTransitionDefinitions)
        : base(extent) {

        AddChildNodes(TransitionDefinitions     = transitionDefinitions);
        AddChildNodes(ExitTransitionDefinitions = exitTransitionDefinitions);
    }

    public IReadOnlyList<TransitionDefinitionSyntax>     TransitionDefinitions     { get; }
    public IReadOnlyList<ExitTransitionDefinitionSyntax> ExitTransitionDefinitions { get; }

    private protected override bool PromiseNoDescendantNodeOfSameType => true;

}