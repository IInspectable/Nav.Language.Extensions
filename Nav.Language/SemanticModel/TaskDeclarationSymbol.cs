#region Using Directives

using System.Collections.Generic;
using System.Linq;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Die Implementierung von <see cref="ITaskDeclarationSymbol"/>. Erzeugt und befüllt wird sie im
/// <see cref="TaskDeclarationSymbolBuilder"/>; die Kollektionen (<see cref="ConnectionPoints"/>,
/// <see cref="References"/>) sind hier schreibbar und werden erst über die Interface-Sicht
/// read-only.
/// </summary>
sealed partial class TaskDeclarationSymbol: Symbol, ITaskDeclarationSymbol {

    public TaskDeclarationSymbol(string name, Location location,
                                 TaskDeclarationOrigin origin,
                                 bool isIncluded,
                                 ICodeParameter? codeTaskResult,
                                 MemberDeclarationSyntax? syntax,
                                 string? codeNamespace,
                                 bool codeNotImplemented): base(name, location) {
        Origin           = origin;
        Syntax           = syntax;
        IsIncluded       = isIncluded;
        References       = new List<ITaskNodeSymbol>();
        ConnectionPoints = new SymbolCollection<ConnectionPointSymbol>();

        CodeNamespace      = codeNamespace ?? string.Empty;
        CodeNotImplemented = codeNotImplemented;
        CodeTaskResult     = codeTaskResult;
    }

    /// <inheritdoc/>
    public override SyntaxTree? SyntaxTree => Syntax?.SyntaxTree;

    /// <inheritdoc/>
    public CodeGenerationUnit? CodeGenerationUnit { get; private set; }

    /// <summary>Die Verbindungspunkte in schreibbarer Form — befüllt vom <see cref="TaskDeclarationSymbolBuilder"/>.</summary>
    public SymbolCollection<ConnectionPointSymbol> ConnectionPoints { get; }
    /// <summary>Die referenzierenden Task-Knoten in schreibbarer Form — befüllt vom <see cref="TaskDefinitionSymbolBuilder"/> beim Binden der Task-Knoten.</summary>
    public List<ITaskNodeSymbol>                   References       { get; }

    IReadOnlySymbolCollection<IConnectionPointSymbol> ITaskDeclarationSymbol.ConnectionPoints => ConnectionPoints;

    IEnumerable<IInitConnectionPointSymbol> ITaskDeclarationSymbol.Inits() {
        return ConnectionPoints.OfType<IInitConnectionPointSymbol>();
    }

    IEnumerable<IExitConnectionPointSymbol> ITaskDeclarationSymbol.Exits() {
        return ConnectionPoints.OfType<IExitConnectionPointSymbol>();
    }

    IEnumerable<IEndConnectionPointSymbol> ITaskDeclarationSymbol.Ends() {
        return ConnectionPoints.OfType<IEndConnectionPointSymbol>();
    }

    IReadOnlyList<ITaskNodeSymbol> ITaskDeclarationSymbol.References => References;

    /// <inheritdoc/>
    public MemberDeclarationSyntax? Syntax { get; }

    /// <inheritdoc/>
    public bool                  IsIncluded { get; }
    /// <inheritdoc/>
    public TaskDeclarationOrigin Origin     { get; }

    /// <inheritdoc/>
    public string CodeNamespace { get; }

    /// <inheritdoc/>
    public bool CodeNotImplemented { get; }

    /// <inheritdoc/>
    public ICodeParameter? CodeTaskResult { get; }

    /// <summary>
    /// Liefert diese Deklaration samt ihrer Verbindungspunkte. Hierüber sammelt der
    /// <see cref="CodeGenerationUnitBuilder"/> die Symbole der Datei ein — allerdings nur für
    /// nicht-inkludierte <c>taskref</c>-Deklarationen (die Symbole einer Definition liefert
    /// <see cref="TaskDefinitionSymbol.SymbolsAndSelf"/>).
    /// </summary>
    public IEnumerable<ISymbol> SymbolsAndSelf() {
        yield return this;

        foreach (var symbol in ConnectionPoints) {
            yield return symbol;
        }
    }

    /// <summary>
    /// Setzt nachträglich die Rückreferenz auf das semantische Modell. Der
    /// <see cref="CodeGenerationUnitBuilder"/> ruft dies am Ende des Modellbaus auf — nur für
    /// Deklarationen der eigenen Datei; bei inkludierten Deklarationen bleibt
    /// <see cref="CodeGenerationUnit"/> <c>null</c>.
    /// </summary>
    internal void FinalConstruct(CodeGenerationUnit codeGenerationUnit) {
        CodeGenerationUnit = codeGenerationUnit;
    }

    /// <summary>
    /// Erstellt eine Kopie dieser Deklaration samt ihrer Connection Points.
    /// </summary>
    /// <remarks>
    /// Inkludierte Deklarationen werden je Include-Datei nur einmal extrahiert und als Prototypen
    /// gecacht (siehe <see cref="TaskDeclarationSymbolBuilder"/>). Da die inkludierende Datei
    /// Zustand an der Deklaration verdrahtet (<see cref="References"/> der Task-Knoten), erhält
    /// jede inkludierende Datei ihren eigenen Klon; die unveränderlichen Bestandteile
    /// (Location, Syntax, <see cref="CodeTaskResult"/>) werden geteilt.
    /// </remarks>
    internal TaskDeclarationSymbol Clone() {

        var clone = new TaskDeclarationSymbol(
            name: Name,
            location: Location,
            origin: Origin,
            isIncluded: IsIncluded,
            codeTaskResult: CodeTaskResult,
            syntax: Syntax,
            codeNamespace: CodeNamespace,
            codeNotImplemented: CodeNotImplemented);

        foreach (var connectionPoint in ConnectionPoints) {
            clone.ConnectionPoints.Add(connectionPoint.WithTaskDeclaration(clone));
        }

        return clone;
    }

}
