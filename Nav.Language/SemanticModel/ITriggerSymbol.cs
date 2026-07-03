namespace Pharmatechnik.Nav.Language;

public interface ITriggerSymbol: ISymbol {

    ITriggerTransition Transition { get; }

    bool IsSignalTrigger      { get; }
    bool IsSpontaneousTrigger { get; }

}

// Für den visitor ist es günstiger, explizite Interfaces zu haben..
public interface ISignalTriggerSymbol: ITriggerSymbol {

    IdentifierOrStringSyntax Syntax { get; }

    new ITriggerTransition Transition { get; }

}

public interface ISpontaneousTriggerSymbol: ITriggerSymbol {

    SpontaneousTriggerSyntax Syntax { get; }

}
