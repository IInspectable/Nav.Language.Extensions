#region Using Directives

using System.Collections.Generic;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.Symbols;

#endregion

namespace Pharmatechnik.Nav.Language.Mcp.Tools;

/// <summary>
/// Gemeinsame Namen→Symbol-Auflösung mit Disambiguierung für die name-basierten Tools (goto, references,
/// rename, code_actions). Kapselt <see cref="NavSymbolSearch.FindByName"/> samt der drei Ausgänge
/// „eindeutig / nicht gefunden / mehrdeutig" und die zugehörigen, agentenfreundlichen Meldungen.
/// </summary>
static class NavNameResolution {

    public enum Status {
        Resolved,
        NotFound,
        Ambiguous
    }

    public static Status Resolve(CodeGenerationUnit unit, string name, string? taskScope,
                                 out ISymbol? symbol, out IReadOnlyList<ISymbol> candidates) {

        candidates = NavSymbolSearch.FindByName(unit, name, taskScope);

        if (candidates.Count == 0) {
            symbol = null;
            return Status.NotFound;
        }

        if (candidates.Count > 1) {
            symbol = null;
            return Status.Ambiguous;
        }

        symbol = candidates[0];
        return Status.Resolved;
    }

    public static string NotFoundMessage(string name, string path) =>
        $"No task or node named '{name}' found in {path}.";

    public static string AmbiguousMessage(string name) =>
        $"The name '{name}' is ambiguous (e.g. the same node name exists in several tasks). " +
        "Pass the 'task' parameter to scope it to one task definition; see the candidates.";
}
