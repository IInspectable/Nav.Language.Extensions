#nullable enable

#region Using Directives

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

#endregion

namespace Pharmatechnik.Nav.Language;

public static class CodeGenerationUnitExtensions {

    public static ITaskDefinitionSymbol? TryFindTaskDefinition(this CodeGenerationUnit? codeGenerationUnit, string? taskName) {
        return codeGenerationUnit?.TaskDefinitions.TryFindSymbol(taskName);
    }

    public static IEnumerable<string> GetCodeUsingNamespaces(this CodeGenerationUnit? codeGenerationUnit) {

        if (codeGenerationUnit == null) {
            return ImmutableList<string>.Empty;
        }

        return codeGenerationUnit.Syntax
                                 .CodeUsings
                                 .Select(cu => cu.Namespace?.Text)
                                 .OfType<string>();
    }

    public static IEnumerable<string> ToSortedNamespaces(this IEnumerable<string?> usings) {
        return usings.OfType<string>()
                     .Distinct()
                     .OrderBy(ns => ns.Length)
                     .ThenBy(ns => ns)
                     .ToImmutableList();
    }

}
