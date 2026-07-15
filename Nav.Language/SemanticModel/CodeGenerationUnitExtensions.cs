#region Using Directives

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Null-tolerante Lookups auf der <see cref="CodeGenerationUnit"/> und die Aufbereitung der
/// <c>[using …]</c>-Namespaces für die Codegenerierung.
/// </summary>
public static class CodeGenerationUnitExtensions {

    /// <summary>
    /// Liefert die <c>task</c>-Definition mit dem angegebenen Namen aus
    /// <see cref="CodeGenerationUnit.TaskDefinitions"/>, oder <c>null</c> — auch wenn Modell oder
    /// Name <c>null</c> sind.
    /// </summary>
    /// <param name="codeGenerationUnit">Das zu durchsuchende Modell, oder <c>null</c>.</param>
    /// <param name="taskName">Der gesuchte Task-Name, oder <c>null</c>.</param>
    public static ITaskDefinitionSymbol? TryFindTaskDefinition(this CodeGenerationUnit? codeGenerationUnit, string? taskName) {
        return codeGenerationUnit?.TaskDefinitions.TryFindSymbol(taskName);
    }

    /// <summary>
    /// Liefert die Namespace-Texte der <c>[using …]</c>-Deklarationen am Datei-Kopf
    /// (<see cref="CodeGenerationUnitSyntax.CodeUsings"/>); Deklarationen ohne Namespace-Angabe
    /// werden ausgelassen, bei <c>null</c> ist das Ergebnis leer. Grundlage der using-Direktiven
    /// in den CodeModels der Codegenerierung (dort typisch gefolgt von
    /// <see cref="ToSortedNamespaces"/>).
    /// </summary>
    /// <param name="codeGenerationUnit">Das Modell, dessen Usings geliefert werden, oder <c>null</c>.</param>
    public static IEnumerable<string> GetCodeUsingNamespaces(this CodeGenerationUnit? codeGenerationUnit) {

        if (codeGenerationUnit == null) {
            return ImmutableList<string>.Empty;
        }

        return codeGenerationUnit.Syntax
                                 .CodeUsings
                                 .Select(cu => cu.Namespace?.Text)
                                 .OfType<string>();
    }

    /// <summary>
    /// Dedupliziert die Namespaces und sortiert sie erst nach Länge, dann alphabetisch —
    /// die Sortierordnung der using-Direktiven im generierten Code; <c>null</c>-Einträge werden
    /// entfernt.
    /// </summary>
    /// <param name="usings">Die zu sortierenden Namespaces; <c>null</c>-Einträge sind erlaubt.</param>
    public static IEnumerable<string> ToSortedNamespaces(this IEnumerable<string?> usings) {
        return usings.OfType<string>()
                     .Distinct()
                     .OrderBy(ns => ns.Length)
                     .ThenBy(ns => ns)
                     .ToImmutableList();
    }

}
