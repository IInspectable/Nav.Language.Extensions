#nullable enable

namespace Pharmatechnik.Nav.Language;

abstract class ConnectionPointSymbol: Symbol, IConnectionPointSymbol {

    protected ConnectionPointSymbol(SyntaxTree syntaxTree, ConnectionPointKind kind, string name, Location location, TaskDeclarationSymbol taskDeclaration)
        : base(name, location) {

        SyntaxTree      = syntaxTree;
        Kind            = kind;
        TaskDeclaration = taskDeclaration;
    }

    public override SyntaxTree SyntaxTree { get; }

    public ConnectionPointKind    Kind            { get; }
    public ITaskDeclarationSymbol TaskDeclaration { get; }

    /// <summary>
    /// Erstellt eine Kopie dieses Connection Points, die an die angegebene Task-Deklaration
    /// gebunden ist. Alle übrigen Bestandteile (Name, Location, Syntax) sind unveränderlich
    /// und werden geteilt.
    /// </summary>
    internal abstract ConnectionPointSymbol WithTaskDeclaration(TaskDeclarationSymbol taskDeclaration);

}

sealed partial class InitConnectionPointSymbol: ConnectionPointSymbol, IInitConnectionPointSymbol {

    public InitConnectionPointSymbol(string name, Location location, InitNodeDeclarationSyntax syntax, TaskDeclarationSymbol taskDeclaration)
        : base(syntax.SyntaxTree, ConnectionPointKind.Init, name, location, taskDeclaration) {
        Syntax = syntax;
    }

    public InitNodeDeclarationSyntax Syntax { get; }

    internal override ConnectionPointSymbol WithTaskDeclaration(TaskDeclarationSymbol taskDeclaration) {
        return new InitConnectionPointSymbol(Name, Location, Syntax, taskDeclaration);
    }

}

sealed partial class ExitConnectionPointSymbol: ConnectionPointSymbol, IExitConnectionPointSymbol {

    public ExitConnectionPointSymbol(string name, Location location, ExitNodeDeclarationSyntax syntax, TaskDeclarationSymbol taskDeclaration)
        : base(syntax.SyntaxTree, ConnectionPointKind.Exit, name, location, taskDeclaration) {
        Syntax = syntax;
    }

    public ExitNodeDeclarationSyntax Syntax { get; }

    internal override ConnectionPointSymbol WithTaskDeclaration(TaskDeclarationSymbol taskDeclaration) {
        return new ExitConnectionPointSymbol(Name, Location, Syntax, taskDeclaration);
    }

}

sealed partial class EndConnectionPointSymbol: ConnectionPointSymbol, IEndConnectionPointSymbol {

    public EndConnectionPointSymbol(string name, Location location, EndNodeDeclarationSyntax syntax, TaskDeclarationSymbol taskDeclaration)
        : base(syntax.SyntaxTree, ConnectionPointKind.End, name, location, taskDeclaration) {
        Syntax = syntax;
    }

    public EndNodeDeclarationSyntax Syntax { get; }

    internal override ConnectionPointSymbol WithTaskDeclaration(TaskDeclarationSymbol taskDeclaration) {
        return new EndConnectionPointSymbol(Name, Location, Syntax, taskDeclaration);
    }

}
