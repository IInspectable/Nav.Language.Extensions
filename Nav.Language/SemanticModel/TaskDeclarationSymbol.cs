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

    IReadOnlySymbolCollection<IInitConnectionPointSymbol> ITaskDeclarationSymbol.Inits() {
        return new SymbolCollection<IInitConnectionPointSymbol>(ConnectionPoints.OfType<IInitConnectionPointSymbol>());
    }

    IReadOnlySymbolCollection<IExitConnectionPointSymbol> ITaskDeclarationSymbol.Exits() {
        return new SymbolCollection<IExitConnectionPointSymbol>(ConnectionPoints.OfType<IExitConnectionPointSymbol>());
    }

    IReadOnlySymbolCollection<IEndConnectionPointSymbol> ITaskDeclarationSymbol.Ends() {
        return new SymbolCollection<IEndConnectionPointSymbol>(ConnectionPoints.OfType<IEndConnectionPointSymbol>());
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

}