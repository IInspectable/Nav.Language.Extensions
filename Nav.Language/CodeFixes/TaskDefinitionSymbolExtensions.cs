#region Using Directives

using System;
using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes;

/// <summary>
/// Interne Hilfen für Fixes, die innerhalb einer Task-Definition einen neuen Knotennamen einführen (etwa
/// beim Umbenennen). Prüft einen Kandidatennamen gegen die Nav-Regeln und die bereits vergebenen Namen.
/// </summary>
static class TaskDefinitionSymbolExtensions {

    /// <summary>
    /// Prüft, ob <paramref name="nodeName"/> ein zulässiger neuer Knotenname innerhalb der Task-Definition
    /// ist (führender/abschließender Whitespace wird zuvor entfernt). Liefert <c>null</c>, wenn der Name gültig
    /// ist; andernfalls die passende Fehlermeldung: die Meldung von
    /// <see cref="DiagnosticDescriptors.Semantic"/>-<c>Nav2000</c>, wenn es kein gültiger Bezeichner ist, bzw.
    /// von <c>Nav0022</c>, wenn bereits ein Knoten dieses Namens deklariert ist.
    /// </summary>
    /// <param name="taskDefinitionSymbol">Die Task-Definition, deren bereits vergebene Knotennamen geprüft werden (darf <c>null</c> sein).</param>
    /// <param name="nodeName">Der zu prüfende Kandidatenname (darf <c>null</c> sein).</param>
    /// <returns><c>null</c> bei gültigem Namen, sonst der Fehlermeldungstext.</returns>
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

    /// <summary>
    /// Sammelt die Namen aller in der Task-Definition deklarierten Knoten (leere Namen ausgenommen); bei
    /// <c>null</c>-Symbol eine leere Menge.
    /// </summary>
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