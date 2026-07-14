namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Basisimplementierung von <see cref="ITriggerSymbol"/>. Trigger entstehen ausschließlich im
/// <see cref="TriggerSymbolBuilder"/> aus dem <see cref="TransitionDefinitionSyntax.Trigger"/>-Teil
/// einer Transition.
/// </summary>
abstract class TriggerSymbol: Symbol, ITriggerSymbol {

    /// <summary>Initialisiert die Basisklasse mit Name und Fundstelle des Triggers.</summary>
    protected TriggerSymbol(string name, Location location)
        : base(name, location) {
    }

    // Wird im Ctor der Transition während der Initialisierung gesetzt — in der "freien Wildbahn"
    // darf der Null-Fall nicht auftreten.
    /// <inheritdoc/>
    /// <remarks>
    /// Wird während der Modell-Konstruktion vom Konstruktor der Trigger-Transition gesetzt
    /// (interner Setter); am fertigen Semantikmodell nie <c>null</c>.
    /// </remarks>
    public ITriggerTransition Transition { get; internal set; } = null!;

    /// <inheritdoc/>
    public abstract bool IsSignalTrigger      { get; }
    /// <inheritdoc/>
    public abstract bool IsSpontaneousTrigger { get; }

}

/// <summary>Implementierung von <see cref="ISignalTriggerSymbol"/> — ein Signal-Trigger (<c>on Signal</c>).</summary>
sealed partial class SignalTriggerSymbol: TriggerSymbol, ISignalTriggerSymbol {

    /// <summary>Initialisiert den Signal-Trigger; der Name ist der Signal-Name aus dem Quelltext.</summary>
    public SignalTriggerSymbol(string name, Location location, IdentifierOrStringSyntax syntax)
        : base(name, location) {
        Syntax = syntax;
    }

    /// <inheritdoc/>
    public override SyntaxTree SyntaxTree => Syntax.SyntaxTree;

    /// <inheritdoc/>
    public IdentifierOrStringSyntax Syntax { get; }

    /// <inheritdoc/>
    public override bool IsSignalTrigger      => true;
    /// <inheritdoc/>
    public override bool IsSpontaneousTrigger => false;

}

/// <summary>Implementierung von <see cref="ISpontaneousTriggerSymbol"/> — ein spontaner Übergang (<c>spontaneous</c>/<c>spont</c>).</summary>
sealed partial class SpontaneousTriggerSymbol: TriggerSymbol, ISpontaneousTriggerSymbol {

    /// <summary>
    /// Initialisiert den spontanen Trigger; als Name dient stets das kanonische Literal
    /// <see cref="SpontaneousTriggerSyntax.Keyword"/>, auch bei der Kurzform <c>spont</c>.
    /// </summary>
    public SpontaneousTriggerSymbol(Location location, SpontaneousTriggerSyntax syntax)
        : base(SpontaneousTriggerSyntax.Keyword, location) {
        Syntax = syntax;
    }

    /// <inheritdoc/>
    public override SyntaxTree SyntaxTree => Syntax.SyntaxTree;

    /// <inheritdoc/>
    public SpontaneousTriggerSyntax Syntax { get; }

    /// <inheritdoc/>
    public override bool IsSignalTrigger      => false;
    /// <inheritdoc/>
    public override bool IsSpontaneousTrigger => true;

}
