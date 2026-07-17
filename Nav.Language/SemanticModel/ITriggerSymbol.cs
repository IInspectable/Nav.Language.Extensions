namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Symbol des Triggers einer Trigger-Transition — in <c>View --&gt; Ziel on Speichern;</c> das
/// Signal <c>Speichern</c>. Zwei Arten: ein Signal-Trigger <c>on Signal</c>
/// (<see cref="ISignalTriggerSymbol"/>) oder eine spontane Transition <c>spontaneous</c>/<c>spont</c>
/// (<see cref="ISpontaneousTriggerSymbol"/>). Innerhalb einer Transition muss der Trigger-Name
/// eindeutig sein (Nav0026); je Quellknoten darf jeder Trigger nur eine ausgehende Kante
/// auslösen (Nav0023).
/// </summary>
public interface ITriggerSymbol: ISymbol {

    /// <summary>Die Trigger-Transition, die dieser Trigger auslöst.</summary>
    ITriggerTransition Transition { get; }

    /// <summary>Ob dies ein Signal-Trigger (<c>on Signal</c>) ist — das Symbol ist dann ein <see cref="ISignalTriggerSymbol"/>.</summary>
    bool IsSignalTrigger      { get; }
    /// <summary>Ob dies eine spontane Transition (<c>spontaneous</c>/<c>spont</c>) ist — das Symbol ist dann ein <see cref="ISpontaneousTriggerSymbol"/>.</summary>
    bool IsSpontaneousTrigger { get; }

}

// Für den visitor ist es günstiger, explizite Interfaces zu haben..
/// <summary>
/// Ein Signal-Trigger, z.B. <c>on Speichern</c> (<see cref="SignalTriggerSyntax"/>) — der
/// <see cref="ISymbol.Name"/> ist der Signal-Name und bestimmt im generierten Code den Namen der
/// Trigger-Logik-Methode (<c>&lt;Signal&gt;Logic</c>).
/// </summary>
public interface ISignalTriggerSymbol: ITriggerSymbol {

    /// <summary>Der Signal-Name hinter dem <c>on</c>-Schlüsselwort (<see cref="SignalTriggerSyntax.Identifier"/>).</summary>
    IdentifierOrStringSyntax Syntax { get; }

    /// <inheritdoc cref="ITriggerSymbol.Transition"/>
    new ITriggerTransition Transition { get; }

}

/// <summary>
/// Eine spontane Transition ohne explizites Signal, geschrieben als <c>spontaneous</c> oder in der
/// Kurzform <c>spont</c> — der <see cref="ISymbol.Name"/> ist stets das kanonische Literal
/// <see cref="SpontaneousTriggerSyntax.Keyword"/> (<c>"spontaneous"</c>), auch bei der Kurzform.
/// </summary>
public interface ISpontaneousTriggerSymbol: ITriggerSymbol {

    /// <summary>Die zugrunde liegende <c>spontaneous</c>-Syntax.</summary>
    SpontaneousTriggerSyntax Syntax { get; }

}
