#region Using Directives

using System;
using System.Collections.Generic;
using System.Linq;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes.StyleFix; 

/// <summary>
/// Stil-Fix, der die ungenutzten Knoten einer Task-Definition entfernt: alle
/// <see cref="INodeSymbol"/>e der <see cref="ITaskDefinitionSymbol"/>, auf die es keine Referenz gibt und
/// die kein Verbindungspunkt (Init/Exit/End) sind — denn Verbindungspunkte bilden die Schnittstelle des
/// Tasks und dürfen auch ohne Referenz nicht als „ungenutzt" gelöscht werden. Erzeugt die
/// <see cref="Pharmatechnik.Nav.Language.Text.TextChange"/>s, die die Knoten-Deklarationen aus dem
/// Quelltext löschen. Gefunden wird der Fix vom <see cref="RemoveUnusedNodesCodeFixProvider"/>.
/// </summary>
public class RemoveUnusedNodesCodeFix: StyleCodeFix {

    internal RemoveUnusedNodesCodeFix(ITaskDefinitionSymbol taskDefinitionSymbol, CodeFixContext context)
        : base(context) {
        TaskDefinition = taskDefinitionSymbol ?? throw new ArgumentNullException(nameof(taskDefinitionSymbol));
    }

    /// <summary>Die Task-Definition, deren ungenutzte Knoten entfernt werden.</summary>
    public ITaskDefinitionSymbol TaskDefinition { get; }

    /// <summary>Der Anzeigename des Fixes: „Remove Unused Nodes".</summary>
    public override string        Name         => "Remove Unused Nodes";
    /// <inheritdoc/>
    public override CodeFixImpact Impact       => CodeFixImpact.None;
    /// <inheritdoc/>
    public override TextExtent?   ApplicableTo => null;
    /// <inheritdoc/>
    public override CodeFixPrio   Prio         => CodeFixPrio.Medium;

    /// <summary>
    /// Prüft, ob es mindestens einen entfernbaren Knoten gibt (siehe <see cref="GetCandidates"/>).
    /// </summary>
    /// <returns><c>true</c>, wenn der Fix etwas zu tun hat.</returns>
    internal bool CanApplyFix() {
        return GetCandidates().Any();
    }

    /// <summary>
    /// Die entfernbaren Knoten der <see cref="TaskDefinition"/>: referenzlos und kein Verbindungspunkt
    /// (<see cref="NodeSymbolExtension.IsConnectionPoint"/>).
    /// </summary>
    IEnumerable<INodeSymbol> GetCandidates() {
        // Verbindungspunkte stellen die Schnittstelle zum Task dar, und können
        // von daher nicht als "unbnutzt, und entfernbar" behandelt werden, auch
        // wenn es bisweilen keine Referenzen auf diese gibt.
        return TaskDefinition.NodeDeclarations.Where(n => n.References.Count == 0 && 
                                                          !n.IsConnectionPoint());
    }
        
    /// <summary>
    /// Liefert die <see cref="Pharmatechnik.Nav.Language.Text.TextChange"/>s, die alle entfernbaren Knoten
    /// (siehe <see cref="GetCandidates"/>) aus dem Quelltext löschen.
    /// </summary>
    /// <returns>Das Lösch-Edit-Set (leer, wenn es keine Kandidaten gibt).</returns>
    public override IList<TextChange> GetTextChanges() {

        var textChanges = new List<TextChange?>();
        foreach (var textChange in GetCandidates().SelectMany(c => GetRemoveSyntaxNodeChanges(c.Syntax))) {
            textChanges.Add(textChange);
        }

        return textChanges.OfType<TextChange>().ToList();
    }

}