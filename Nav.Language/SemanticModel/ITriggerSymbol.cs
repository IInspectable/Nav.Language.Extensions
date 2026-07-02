using JetBrains.Annotations;

namespace Pharmatechnik.Nav.Language; 

public interface ITriggerSymbol: ISymbol {

    [NotNull]
    ITriggerTransition Transition { get; }

    bool IsSignalTrigger      { get; }
    bool IsSpontaneousTrigger { get; }

}

// Für den visitor ist es günstiger, explizite Interfaces zu haben..
public interface ISignalTriggerSymbol: ITriggerSymbol {

    [NotNull]
    IdentifierOrStringSyntax Syntax { get; }

    [NotNull]
    new ITriggerTransition Transition { get; }

}

public interface ISpontaneousTriggerSymbol: ITriggerSymbol {

    [NotNull]
    SpontaneousTriggerSyntax Syntax { get; }

}