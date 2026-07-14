#region Using Directives

using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Die Herkunft einer Task-Deklaration (<see cref="ITaskDeclarationSymbol.Origin"/>): explizit aus
/// einer <c>taskref</c>-Deklaration oder implizit aus einer <c>task</c>-Definition.
/// </summary>
public enum TaskDeclarationOrigin {

    /// <summary>Entstanden aus einer <c>taskref Name { … }</c>-Deklaration (<see cref="TaskDeclarationSyntax"/>).</summary>
    TaskDeclaration,
    /// <summary>Implizit entstanden aus einer <c>task Name { … }</c>-Definition (<see cref="TaskDefinitionSyntax"/>).</summary>
    TaskDefinition

}

/// <summary>
/// Symbol einer Task-Deklaration — die von außen sichtbare Schnittstelle eines Tasks, bestehend
/// aus seinen Verbindungspunkten (<see cref="ConnectionPoints"/>) und optionalen Code-Angaben
/// (<c>[namespaceprefix …]</c>, <c>[notimplemented]</c>, <c>[result …]</c>). Sie entsteht auf
/// zwei Wegen (<see cref="Origin"/>): explizit aus einer <c>taskref</c>-Deklaration, die einen
/// anderweitig definierten Task bekannt macht,
/// <code>
/// taskref Auswahl {
///     init I1;
///     exit Ok;
/// }
/// </code>
/// oder implizit aus einer <c>task</c>-Definition, deren Schnittstelle sie zusammenfasst
/// (<see cref="ITaskDefinitionSymbol.AsTaskDeclaration"/>). Task-Knoten binden den Task über
/// seine Deklaration ein (<see cref="ITaskNodeSymbol.Declaration"/>); Namensduplikate meldet
/// Nav0020. Konstruktionsstelle ist der <c>TaskDeclarationSymbolBuilder</c>.
/// </summary>
public interface ITaskDeclarationSymbol: ISymbol {

    /// <summary>
    /// Die zugrunde liegende Deklarations-Syntax — je nach <see cref="Origin"/> eine
    /// <see cref="TaskDeclarationSyntax"/> (<c>taskref</c>) oder <see cref="TaskDefinitionSyntax"/>
    /// (<c>task</c>). Ist nur dann null, wenn <see cref="IsIncluded"/> true, da wir keine
    /// Syntaxbäume von anderen nav-Dateien im Speicher halten wollen (verworfen wird die Syntax
    /// inkludierter <c>taskref</c>-Deklarationen).
    /// </summary>
    MemberDeclarationSyntax? Syntax { get; }

    /// <summary>
    /// Das semantische Modell der Datei, zu der diese Deklaration gehört. Ist nur dann null, wenn
    /// <see cref="IsIncluded"/> true, da wir keine Semantikmodelle von anderen nav-Dateien im
    /// Speicher halten wollen.
    /// </summary>
    CodeGenerationUnit? CodeGenerationUnit { get; }

    /// <summary>
    /// Die Verbindungspunkte der Task-Schnittstelle: Einstiegspunkte (<c>init</c>), benannte
    /// Ausgänge (<c>exit</c>) und der reguläre Abschluss (<c>end</c>) — siehe
    /// <see cref="IConnectionPointSymbol"/>; Namensduplikate meldet Nav0021.
    /// </summary>
    IReadOnlySymbolCollection<IConnectionPointSymbol> ConnectionPoints { get; }

    /// <summary>Die <c>init</c>-Verbindungspunkte unter den <see cref="ConnectionPoints"/>.</summary>
    IEnumerable<IInitConnectionPointSymbol> Inits();
    /// <summary>Die <c>exit</c>-Verbindungspunkte unter den <see cref="ConnectionPoints"/>.</summary>
    IEnumerable<IExitConnectionPointSymbol> Exits();
    /// <summary>Die <c>end</c>-Verbindungspunkte unter den <see cref="ConnectionPoints"/>.</summary>
    IEnumerable<IEndConnectionPointSymbol> Ends();

    /// <summary>
    /// Alle Task-Knoten (z.B. <c>task Auswahl A1;</c>), die diese Deklaration referenzieren —
    /// verdrahtet beim Binden der Task-Definitionen (<see cref="ITaskNodeSymbol.Declaration"/>);
    /// Grundlage z.B. für Find References.
    /// </summary>
    IReadOnlyList<ITaskNodeSymbol> References { get; }

    /// <summary>
    /// Gibt an, ob die Deklaration aus einer inkludierten nav-Datei stammt (per Include-Direktive
    /// <c>taskref "datei.nav";</c> eingebunden).
    /// </summary>
    bool IsIncluded { get; }

    /// <summary>
    /// Gibt an, ob die Deklaration aus einer reinen Deklaration (taskref ...) oder einer Definition (task ...) entstammt.
    /// </summary>
    TaskDeclarationOrigin Origin { get; }

    /// <summary>
    /// Der C#-Namespace des Tasks: bei einer <c>taskref</c>-Deklaration der Wert ihrer
    /// <c>[namespaceprefix …]</c>-Angabe, bei einer Definition der <c>[namespaceprefix …]</c> aus
    /// dem Kopf der definierenden Datei; leer, wenn nicht angegeben.
    /// </summary>
    string CodeNamespace { get; }

    /// <summary>
    /// Ob die Deklaration als <c>[notimplemented]</c> markiert ist — der referenzierte Task ist
    /// (noch) nicht implementiert, der Codegen übergeht so markierte Task-Knoten. Nur an
    /// <c>taskref</c>-Deklarationen möglich; für Deklarationen aus Definitionen stets <c>false</c>.
    /// </summary>
    bool CodeNotImplemented { get; }

    /// <summary>
    /// Der Ergebniswert des Tasks aus der <c>[result Typ name]</c>-Deklaration (siehe
    /// <see cref="ICodeParameter"/>) — <c>null</c>, wenn keine <c>[result …]</c>-Angabe mit
    /// Typ vorhanden ist.
    /// </summary>
    ICodeParameter? CodeTaskResult { get; }

}
