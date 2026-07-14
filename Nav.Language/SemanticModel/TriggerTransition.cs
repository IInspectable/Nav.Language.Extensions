using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Semantic-Model-Umsetzung von <see cref="ITriggerTransition"/> — die Transition mit GUI-Knoten
/// (View/Dialog) als Quelle, ausgelöst von Triggern, z.B. <c>View --&gt; Ziel on Speichern;</c>.
/// </summary>
sealed class TriggerTransition: Transition, ITriggerTransition {

    /// <summary>
    /// Erzeugt die Trigger-Transition (Verankerung der Referenzen siehe Basis-Konstruktor) und setzt
    /// sich als <see cref="TriggerSymbol.Transition"/> an jedem ihrer <see cref="Triggers"/>.
    /// </summary>
    public TriggerTransition(TransitionDefinitionSyntax syntax,
                             ITaskDefinitionSymbol containingTask,
                             GuiNodeReferenceSymbol? sourceReference,
                             EdgeModeSymbol? edgeMode,
                             NodeReferenceSymbol? targetReference,
                             ContinuationTransition? continuationTransition,
                             SymbolCollection<TriggerSymbol>? triggers)
        : base(syntax, containingTask, sourceReference, edgeMode, targetReference, continuationTransition) {

        Triggers = triggers ?? new SymbolCollection<TriggerSymbol>();

        foreach (var trigger in Triggers) {
            trigger.Transition = this;
        }
    }

    /// <inheritdoc/>
    public IGuiNodeReferenceSymbol? GuiNodeSourceReference => (IGuiNodeReferenceSymbol?) SourceReference;

    IReadOnlySymbolCollection<ITriggerSymbol> ITriggerTransition.Triggers => Triggers;

    /// <summary>
    /// Die Auslöser dieser Transition als konkrete Symbol-Collection — nie <c>null</c>
    /// (ohne Trigger im Quelltext eine leere Collection).
    /// </summary>
    public SymbolCollection<TriggerSymbol> Triggers { get; }

    /// <summary>
    /// Liefert die Teil-Symbole der Basis (Quelle, Kantenmodus, Ziel, Continuation) und zusätzlich
    /// die <see cref="Triggers"/>.
    /// </summary>
    public override IEnumerable<ISymbol> Symbols() {

        foreach (var symbol in base.Symbols()) {
            yield return symbol;
        }

        foreach (var trigger in Triggers) {
            yield return trigger;
        }
    }

}
