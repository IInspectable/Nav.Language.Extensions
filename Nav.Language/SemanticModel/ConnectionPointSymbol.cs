namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Basisimplementierung von <see cref="IConnectionPointSymbol"/>. Verbindungspunkte entstehen
/// ausschließlich im <see cref="TaskDeclarationSymbolBuilder"/> aus den
/// <c>init</c>/<c>exit</c>/<c>end</c>-Deklarationen einer <c>taskref</c>-Deklaration bzw. einer
/// <c>task</c>-Definition.
/// </summary>
abstract class ConnectionPointSymbol: Symbol, IConnectionPointSymbol {

    /// <summary>Initialisiert die Basisklasse.</summary>
    /// <param name="syntaxTree">Der Syntaxbaum, in dem der Verbindungspunkt deklariert ist.</param>
    /// <param name="kind">Die Art des Verbindungspunkts (Einstieg, Ausgang oder Abschluss).</param>
    /// <param name="name">Der Name des Verbindungspunkts; bei namenlosen Deklarationen das Schlüsselwort selbst.</param>
    /// <param name="location">Die Fundstelle des Namens bzw. Schlüsselworts.</param>
    /// <param name="taskDeclaration">Die Task-Deklaration, zu deren Schnittstelle der Verbindungspunkt gehört.</param>
    protected ConnectionPointSymbol(SyntaxTree syntaxTree, ConnectionPointKind kind, string name, Location location, TaskDeclarationSymbol taskDeclaration)
        : base(name, location) {

        SyntaxTree      = syntaxTree;
        Kind            = kind;
        TaskDeclaration = taskDeclaration;
    }

    /// <inheritdoc/>
    public override SyntaxTree SyntaxTree { get; }

    /// <inheritdoc/>
    public ConnectionPointKind    Kind            { get; }
    /// <inheritdoc/>
    public ITaskDeclarationSymbol TaskDeclaration { get; }

    /// <summary>
    /// Erstellt eine Kopie dieses Connection Points, die an die angegebene Task-Deklaration
    /// gebunden ist. Alle übrigen Bestandteile (Name, Location, Syntax) sind unveränderlich
    /// und werden geteilt.
    /// </summary>
    internal abstract ConnectionPointSymbol WithTaskDeclaration(TaskDeclarationSymbol taskDeclaration);

}

/// <summary>Implementierung von <see cref="IInitConnectionPointSymbol"/> — ein Einstiegspunkt (<c>init</c>).</summary>
sealed partial class InitConnectionPointSymbol: ConnectionPointSymbol, IInitConnectionPointSymbol {

    /// <summary>Initialisiert den <c>init</c>-Verbindungspunkt (<see cref="ConnectionPointKind.Init"/>).</summary>
    public InitConnectionPointSymbol(string name, Location location, InitNodeDeclarationSyntax syntax, TaskDeclarationSymbol taskDeclaration)
        : base(syntax.SyntaxTree, ConnectionPointKind.Init, name, location, taskDeclaration) {
        Syntax = syntax;
    }

    /// <inheritdoc/>
    public InitNodeDeclarationSyntax Syntax { get; }

    /// <inheritdoc/>
    internal override ConnectionPointSymbol WithTaskDeclaration(TaskDeclarationSymbol taskDeclaration) {
        return new InitConnectionPointSymbol(Name, Location, Syntax, taskDeclaration);
    }

}

/// <summary>Implementierung von <see cref="IExitConnectionPointSymbol"/> — ein benannter Ausgang (<c>exit</c>).</summary>
sealed partial class ExitConnectionPointSymbol: ConnectionPointSymbol, IExitConnectionPointSymbol {

    /// <summary>Initialisiert den <c>exit</c>-Verbindungspunkt (<see cref="ConnectionPointKind.Exit"/>).</summary>
    public ExitConnectionPointSymbol(string name, Location location, ExitNodeDeclarationSyntax syntax, TaskDeclarationSymbol taskDeclaration)
        : base(syntax.SyntaxTree, ConnectionPointKind.Exit, name, location, taskDeclaration) {
        Syntax = syntax;
    }

    /// <inheritdoc/>
    public ExitNodeDeclarationSyntax Syntax { get; }

    /// <inheritdoc/>
    internal override ConnectionPointSymbol WithTaskDeclaration(TaskDeclarationSymbol taskDeclaration) {
        return new ExitConnectionPointSymbol(Name, Location, Syntax, taskDeclaration);
    }

}

/// <summary>Implementierung von <see cref="IEndConnectionPointSymbol"/> — der reguläre Abschluss (<c>end</c>).</summary>
sealed partial class EndConnectionPointSymbol: ConnectionPointSymbol, IEndConnectionPointSymbol {

    /// <summary>Initialisiert den <c>end</c>-Verbindungspunkt (<see cref="ConnectionPointKind.End"/>).</summary>
    public EndConnectionPointSymbol(string name, Location location, EndNodeDeclarationSyntax syntax, TaskDeclarationSymbol taskDeclaration)
        : base(syntax.SyntaxTree, ConnectionPointKind.End, name, location, taskDeclaration) {
        Syntax = syntax;
    }

    /// <inheritdoc/>
    public EndNodeDeclarationSyntax Syntax { get; }

    /// <inheritdoc/>
    internal override ConnectionPointSymbol WithTaskDeclaration(TaskDeclarationSymbol taskDeclaration) {
        return new EndConnectionPointSymbol(Name, Location, Syntax, taskDeclaration);
    }

}
