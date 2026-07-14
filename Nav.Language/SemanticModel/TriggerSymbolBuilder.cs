using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Erzeugt die <see cref="TriggerSymbol"/>e einer Transition aus ihrem
/// <see cref="TransitionDefinitionSyntax.Trigger"/>-Teil — aufgerufen aus dem
/// <see cref="TaskDefinitionSymbolBuilder"/> beim Binden einer Trigger-Transition. Namensgleiche
/// Trigger innerhalb derselben Transition werden gemeldet (Nav0026); nur das erste Vorkommen
/// gelangt in die Ergebnis-Collection.
/// </summary>
sealed class TriggerSymbolBuilder: SyntaxNodeVisitor {

    readonly List<TriggerSymbol> _triggers;
    readonly List<Diagnostic>    _diagnostics;

    /// <summary>Initialisiert den Builder; Diagnostics werden in die übergebene Liste geschrieben.</summary>
    public TriggerSymbolBuilder(List<Diagnostic>? diagnostics) {
        _diagnostics = diagnostics ?? new List<Diagnostic>();
        _triggers    = new List<TriggerSymbol>();
    }

    /// <summary>
    /// Erzeugt die Trigger-Symbole der angegebenen Transition samt der dabei angefallenen
    /// Diagnostics.
    /// </summary>
    /// <param name="transitionDefinitionSyntax">Die Transition, deren Trigger-Teil gebunden wird.</param>
    /// <returns>Die (namens-eindeutigen) Trigger und die Diagnostics (Nav0026 bei Duplikaten).</returns>
    public static (
        SymbolCollection<TriggerSymbol> Triggers,
        IReadOnlyList<Diagnostic> Diagnostics) Build(TransitionDefinitionSyntax transitionDefinitionSyntax) {

        var diagnostics = new List<Diagnostic>();
        var builder     = new TriggerSymbolBuilder(diagnostics);

        var triggers = builder.GetTriggers(transitionDefinitionSyntax);

        return (triggers, diagnostics);
    }

    /// <summary>
    /// Besucht den Trigger-Teil der Transition und sammelt die entstandenen Symbole in eine
    /// namens-eindeutige <see cref="SymbolCollection{T}"/>; für jedes weitere Vorkommen eines
    /// bereits vergebenen Namens wird Nav0026 gemeldet.
    /// </summary>
    public SymbolCollection<TriggerSymbol> GetTriggers(TransitionDefinitionSyntax transitionDefinitionSyntax) {

        if (transitionDefinitionSyntax.Trigger != null) {
            Visit(transitionDefinitionSyntax.Trigger);
        }

        var result = new SymbolCollection<TriggerSymbol>();
        foreach (var trigger in _triggers) {
            var existing = result.TryFindSymbol(trigger.Name);
            if (existing != null) {

                _diagnostics.Add(new Diagnostic(
                                     trigger.Location,
                                     existing.Location,
                                     DiagnosticDescriptors.Semantic.Nav0026TriggerWithName0AlreadyDeclared,
                                     existing.Name));

            } else {
                result.Add(trigger);
            }
        }

        return result;
    }

    /// <summary>
    /// Erzeugt aus <c>spontaneous</c>/<c>spont</c> ein <see cref="SpontaneousTriggerSymbol"/> —
    /// der Symbol-Name ist stets das kanonische Literal <see cref="SpontaneousTriggerSyntax.Keyword"/>.
    /// </summary>
    public override void VisitSpontaneousTrigger(SpontaneousTriggerSyntax spontaneousTriggerSyntax) {
        var location = spontaneousTriggerSyntax.GetLocation();
        var trigger  = new SpontaneousTriggerSymbol(location, spontaneousTriggerSyntax);
        _triggers.Add(trigger);
    }

    /// <summary>
    /// Erzeugt aus <c>on Signal</c> ein <see cref="SignalTriggerSymbol"/> mit dem Signal-Namen als
    /// Symbol-Name — fehlt der Bezeichner hinter <c>on</c> im Quelltext, entsteht kein Symbol.
    /// </summary>
    public override void VisitSignalTrigger(SignalTriggerSyntax signalTriggerSyntax) {

        if (signalTriggerSyntax.Identifier == null) {
            return;
        }

        var signal   = signalTriggerSyntax.Identifier;
        var location = signal.GetLocation();
        var trigger  = new SignalTriggerSymbol(signal.Text, location, signal);
        _triggers.Add(trigger);

    }

}
