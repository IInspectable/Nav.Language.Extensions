#region Using Directives

using System.Linq;
using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language; 

public static class TaskNodeSymbolExtensions {

    public static bool CodeGenerateAbstractMethod(this IInitNodeSymbol initNode) {
        return initNode?.Syntax.CodeAbstractMethodDeclaration?.Keyword.IsMissing == false;
    }

    public static bool CodeGenerateAbstractMethod(this ITaskNodeSymbol taskNode) {
        return taskNode?.Syntax.CodeAbstractMethodDeclaration?.Keyword.IsMissing == false;
    }

    public static bool CodeNotImplemented(this INodeSymbol taskNode) {
        return (taskNode as ITaskNodeSymbol)?.Declaration?.CodeNotImplemented == true;
    }

    public static bool CodeDoNotInject(this INodeSymbol node) {
        return (node as ITaskNodeSymbol)?.Syntax.CodeDoNotInjectDeclaration?.Keyword.IsMissing == false;
    }

    public static IEnumerable<IConnectionPointSymbol> GetUnconnectedExits(this ITaskNodeSymbol taskNode) {

        if (taskNode.Declaration != null) {

            var expectedExits  = taskNode.Declaration.Exits().OrderBy(cp => cp.Name);
            var connectedExits = GetConnectedExits(taskNode).ToList();

            foreach (var expectedExit in expectedExits) {

                if (!connectedExits.Exists(connectedExit => connectedExit == expectedExit)) {
                    yield return expectedExit;
                }
            }

        }

    }

    public static IEnumerable<IConnectionPointSymbol> GetConnectedExits(this ITaskNodeSymbol taskNode) {

        if (taskNode.Declaration == null) {
            return Enumerable.Empty<IConnectionPointSymbol>();
        }

        return GetConnectedExitsImpl().Distinct();

        IEnumerable<IConnectionPointSymbol> GetConnectedExitsImpl() {
            return taskNode.Outgoings
                           .Select(et => et?.ExitConnectionPointReference?.Declaration)
                           .Where(cps => cps != null);

        }

    }

}