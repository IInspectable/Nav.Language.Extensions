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

}