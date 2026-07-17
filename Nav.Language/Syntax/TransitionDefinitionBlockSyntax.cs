using System;
using System.Collections.Generic;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

/// <summary>
/// Der Verdrahtungs-Teil einer Task-Definition: die Folge aller Transitionen
/// (<see cref="TransitionDefinitionSyntax"/>) und Exit-Transitionen
/// (<see cref="ExitTransitionDefinitionSyntax"/>) hinter den Knoten-Deklarationen. Im Quelltext dürfen
/// sich beide Arten mischen; im Baum werden sie getrennt gruppiert (erst alle Transitionen, dann alle
/// Exit-Transitionen — <b>nicht</b> in Quelltext-Reihenfolge, siehe die Konstruktionsstelle im Parser).
/// </summary>
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

    /// <summary>Alle Transitionen des Blocks (ohne die Exit-Transitionen).</summary>
    public IReadOnlyList<TransitionDefinitionSyntax>     TransitionDefinitions     { get; }
    /// <summary>Alle Exit-Transitionen des Blocks.</summary>
    public IReadOnlyList<ExitTransitionDefinitionSyntax> ExitTransitionDefinitions { get; }

    /// <summary>Ein Transitionsblock schachtelt nie einen weiteren Transitionsblock — erlaubt den vorzeitigen Abbruch der Nachfahren-Suche.</summary>
    private protected override bool PromiseNoDescendantNodeOfSameType => true;

}