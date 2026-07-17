namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Die Art eines Verbindungspunkts (<see cref="IConnectionPointSymbol.Kind"/>): Einstieg
/// (<c>init</c>), benannter Ausgang (<c>exit</c>) oder regulärer Abschluss (<c>end</c>).
/// </summary>
public enum ConnectionPointKind {

    /// <summary>Ein Einstiegspunkt (<c>init</c>) — siehe <see cref="IInitConnectionPointSymbol"/>.</summary>
    Init,
    /// <summary>Ein benannter Ausgang (<c>exit</c>) — siehe <see cref="IExitConnectionPointSymbol"/>.</summary>
    Exit,
    /// <summary>Der reguläre Abschluss (<c>end</c>) — siehe <see cref="IEndConnectionPointSymbol"/>.</summary>
    End

}

/// <summary>
/// Symbol eines Verbindungspunkts — der von außen sichtbaren Schnittstelle eines Tasks, bestehend
/// aus Einstiegspunkten (<c>init</c>), benannten Ausgängen (<c>exit</c>) und dem regulären
/// Abschluss (<c>end</c>):
/// <code>
/// taskref Auswahl {
///     init I1;
///     exit Ok;
///     exit Abbrechen;
/// }
/// </code>
/// Verbindungspunkte entstehen aus den <see cref="ConnectionPointNodeSyntax"/>-Deklarationen einer
/// <c>taskref</c>-Deklaration bzw. einer <c>task</c>-Definition und werden an ihrer
/// <see cref="TaskDeclaration"/> gesammelt (<see cref="ITaskDeclarationSymbol.ConnectionPoints"/>;
/// Namensduplikate meldet Nav0021).
/// </summary>
public interface IConnectionPointSymbol: ISymbol {

    /// <summary>Die Art dieses Verbindungspunkts: Einstieg, Ausgang oder Abschluss.</summary>
    ConnectionPointKind    Kind            { get; }
    /// <summary>Die Task-Deklaration, zu deren Schnittstelle dieser Verbindungspunkt gehört.</summary>
    ITaskDeclarationSymbol TaskDeclaration { get; }

}

// TODO wo ist der Alias?
/// <summary>
/// Ein <c>init</c>-Verbindungspunkt — ein Einstiegspunkt des Tasks, z.B.
/// <c>init I1 [params bool refresh];</c>. Der Name ist optional; fehlt er, dient das Schlüsselwort
/// <c>init</c> selbst als <see cref="ISymbol.Name"/>.
/// </summary>
public interface IInitConnectionPointSymbol: IConnectionPointSymbol {

    /// <summary>Die zugrunde liegende <c>init</c>-Deklaration.</summary>
    InitNodeDeclarationSyntax Syntax { get; }

}

/// <summary>
/// Ein <c>exit</c>-Verbindungspunkt — ein benannter Ausgang des Tasks, z.B. <c>exit Fertig;</c>.
/// Von außen wird er auf der Quellseite einer Exit-Transition angesprochen
/// (<c>TaskKnoten:Fertig --&gt; …</c>, siehe <see cref="IExitConnectionPointReferenceSymbol"/>).
/// </summary>
public interface IExitConnectionPointSymbol: IConnectionPointSymbol {

    /// <summary>Die zugrunde liegende <c>exit</c>-Deklaration.</summary>
    ExitNodeDeclarationSyntax Syntax { get; }

}

/// <summary>
/// Der <c>end</c>-Verbindungspunkt — der reguläre Abschluss des Workflows (<c>end;</c>). Der
/// Knoten ist namenlos; das Schlüsselwort selbst dient als <see cref="ISymbol.Name"/>.
/// </summary>
public interface IEndConnectionPointSymbol: IConnectionPointSymbol {

    /// <summary>Die zugrunde liegende <c>end</c>-Deklaration.</summary>
    EndNodeDeclarationSyntax Syntax { get; }

}
