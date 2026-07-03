#region Using Directives

using System;
using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes;

static class TaskDefinitionSymbolExtensions {

    public static string? ValidateNewNodeName(this ITaskDefinitionSymbol? taskDefinitionSymbol, string? nodeName) {

        nodeName = nodeName?.Trim();

        if (!SyntaxFacts.IsValidIdentifier(nodeName)) {
            return DiagnosticDescriptors.Semantic.Nav2000IdentifierExpected.MessageFormat;
        }

        var declaredNodeNames = taskDefinitionSymbol.GetDeclaredNodeNames();
        // IsValidIdentifier (oben) liefert nur für nicht-null true → nodeName ist hier non-null.
        if (declaredNodeNames.Contains(nodeName!)) {
            return String.Format(DiagnosticDescriptors.Semantic.Nav0022NodeWithName0AlreadyDeclared.MessageFormat, nodeName);
        }

        return null;
    }

    static HashSet<string> GetDeclaredNodeNames(this ITaskDefinitionSymbol? taskDefinitionSymbol) {

        var declaredNodeNames = new HashSet<string>();
        if (taskDefinitionSymbol == null) {
            return declaredNodeNames;
        }

        foreach (var node in taskDefinitionSymbol.NodeDeclarations) {
            var nodeName = node.Name;
            if (!String.IsNullOrEmpty(nodeName)) {
                declaredNodeNames.Add(nodeName);
            }
        }

        return declaredNodeNames;
    }

}