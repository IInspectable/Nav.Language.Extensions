#region Using Directives

using System;
using System.Collections.Generic;
using System.Linq;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes.StyleFix; 

public class RemoveUnusedNodesCodeFix: StyleCodeFix {

    internal RemoveUnusedNodesCodeFix(ITaskDefinitionSymbol taskDefinitionSymbol, CodeFixContext context)
        : base(context) {
        TaskDefinition = taskDefinitionSymbol ?? throw new ArgumentNullException(nameof(taskDefinitionSymbol));
    }

    public ITaskDefinitionSymbol TaskDefinition { get; }

    public override string        Name         => "Remove Unused Nodes";
    public override CodeFixImpact Impact       => CodeFixImpact.None;
    public override TextExtent?   ApplicableTo => null;
    public override CodeFixPrio   Prio         => CodeFixPrio.Medium;

    internal bool CanApplyFix() {
        return GetCandidates().Any();
    }

    IEnumerable<INodeSymbol> GetCandidates() {
        // Verbindungspunkte stellen die Schnittstelle zum Task dar, und können
        // von daher nicht als "unbnutzt, und entfernbar" behandelt werden, auch
        // wenn es bisweilen keine Referenzen auf diese gibt.
        return TaskDefinition.NodeDeclarations.Where(n => n.References.Count == 0 && 
                                                          !n.IsConnectionPoint());
    }
        
    public override IList<TextChange> GetTextChanges() {

        var textChanges = new List<TextChange?>();
        foreach (var textChange in GetCandidates().SelectMany(c => GetRemoveSyntaxNodeChanges(c.Syntax))) {
            textChanges.Add(textChange);
        }

        return textChanges.OfType<TextChange>().ToList();
    }

}