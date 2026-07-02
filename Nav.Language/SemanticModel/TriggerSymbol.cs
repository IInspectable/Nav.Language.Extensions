namespace Pharmatechnik.Nav.Language; 

abstract class TriggerSymbol: Symbol, ITriggerSymbol {

    // ReSharper disable once NotNullMemberIsNotInitialized Transition wird im Ctor der Transition während der Initialisierung gesetzt 
    // In der "freien" Wildbahn" darf hingegen der Null Fall nicht auftreten
    protected TriggerSymbol(string name, Location location)
        : base(name, location) {
    }

    public ITriggerTransition Transition { get; internal set; }

    public abstract bool IsSignalTrigger      { get; }
    public abstract bool IsSpontaneousTrigger { get; }

}

sealed partial class SignalTriggerSymbol: TriggerSymbol, ISignalTriggerSymbol {

    public SignalTriggerSymbol(string name, Location location, IdentifierOrStringSyntax syntax)
        : base(name, location) {
        Syntax = syntax;
    }

    public override SyntaxTree SyntaxTree => Syntax.SyntaxTree;

    public IdentifierOrStringSyntax Syntax { get; }

    public override bool IsSignalTrigger      => true;
    public override bool IsSpontaneousTrigger => false;

}

sealed partial class SpontaneousTriggerSymbol: TriggerSymbol, ISpontaneousTriggerSymbol {

    public SpontaneousTriggerSymbol(Location location, SpontaneousTriggerSyntax syntax)
        : base(SpontaneousTriggerSyntax.Keyword, location) {
        Syntax = syntax;
    }

    public override SyntaxTree SyntaxTree => Syntax.SyntaxTree;

    public SpontaneousTriggerSyntax Syntax { get; }

    public override bool IsSignalTrigger      => false;
    public override bool IsSpontaneousTrigger => true;

}