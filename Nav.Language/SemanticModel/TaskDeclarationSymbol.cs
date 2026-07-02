#region Using Directives

using System.Collections.Generic;
using System.Linq;

using JetBrains.Annotations;

#endregion

namespace Pharmatechnik.Nav.Language; 

sealed partial class TaskDeclarationSymbol: Symbol, ITaskDeclarationSymbol {

    public TaskDeclarationSymbol(string name, Location location,
                                 TaskDeclarationOrigin origin,
                                 bool isIncluded,
                                 ICodeParameter codeTaskResult,
                                 MemberDeclarationSyntax syntax,
                                 [CanBeNull] string codeNamespace,
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

    public override SyntaxTree SyntaxTree => Syntax?.SyntaxTree;

    public CodeGenerationUnit CodeGenerationUnit { get; private set; }

    public SymbolCollection<ConnectionPointSymbol> ConnectionPoints { get; }
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

    [CanBeNull]
    public MemberDeclarationSyntax Syntax { get; }

    public bool                  IsIncluded { get; }
    public TaskDeclarationOrigin Origin     { get; }

    [NotNull]
    public string CodeNamespace { get; }

    public bool CodeNotImplemented { get; }

    [CanBeNull]
    public ICodeParameter CodeTaskResult { get; }

    public IEnumerable<ISymbol> SymbolsAndSelf() {
        yield return this;

        foreach (var symbol in ConnectionPoints) {
            yield return symbol;
        }
    }

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