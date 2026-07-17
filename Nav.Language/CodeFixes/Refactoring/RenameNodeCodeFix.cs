namespace Pharmatechnik.Nav.Language.CodeFixes.Refactoring;

/// <summary>
/// Zwischenbasis für das Umbenennen von <see cref="INodeSymbol"/>-Knoten (Choice, Dialog, View, Exit …)
/// innerhalb einer Task-Definition. Prüft neue Namen gegen den umgebenden Task
/// (<see cref="ContainingTask"/>) mittels
/// <see cref="TaskDefinitionSymbolExtensions.ValidateNewNodeName(ITaskDefinitionSymbol, string)"/>.
/// </summary>
/// <typeparam name="T">Der Knoten-Symboltyp, der umbenannt wird.</typeparam>
abstract class RenameNodeCodeFix<T>: RenameCodeFix<T> where T : class, INodeSymbol {

    protected RenameNodeCodeFix(T symbol, ISymbol originatingSymbol, CodeFixContext context)
        : base(symbol, originatingSymbol, context) {
    }

    /// <summary>Die Task-Definition, in der der umzubenennende Knoten liegt.</summary>
    public ITaskDefinitionSymbol ContainingTask => Symbol.ContainingTask;

    /// <summary>
    /// Prüft <paramref name="symbolName"/> als neuen Knotennamen gegen den <see cref="ContainingTask"/>
    /// (Kollisions- und Gültigkeitsprüfung). Der unveränderte Name gilt als zulässig (<c>null</c>).
    /// </summary>
    public override string? ValidateSymbolName(string? symbolName) {
        // De facto kein Rename, aber OK
        if (symbolName == Symbol.Name) {
            return null;
        }

        return ContainingTask.ValidateNewNodeName(symbolName);
    }

}