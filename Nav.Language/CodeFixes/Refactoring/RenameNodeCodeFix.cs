namespace Pharmatechnik.Nav.Language.CodeFixes.Refactoring;

abstract class RenameNodeCodeFix<T>: RenameCodeFix<T> where T : class, INodeSymbol {

    protected RenameNodeCodeFix(T symbol, ISymbol originatingSymbol, CodeFixContext context)
        : base(symbol, originatingSymbol, context) {
    }

    public ITaskDefinitionSymbol ContainingTask => Symbol.ContainingTask;

    public override string? ValidateSymbolName(string? symbolName) {
        // De facto kein Rename, aber OK
        if (symbolName == Symbol.Name) {
            return null;
        }

        return ContainingTask.ValidateNewNodeName(symbolName);
    }

}