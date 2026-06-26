#region Using Directives

using System;
using System.Collections.Generic;
using System.Linq;

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

    public static Status Resolve(CodeGenerationUnit unit, string name, string? taskScope, string? kind,
                                 out ISymbol? symbol, out IReadOnlyList<ISymbol> candidates) {

        candidates = NavSymbolSearch.FindByName(unit, name, taskScope);

        // Bei Mehrdeutigkeit zusätzlich nach Art eingrenzen. Deckt den Fall ab, den der task-Scope NICHT lösen
        // kann: eine Task und ein gleichnamiger Knoten in eben dieser Task (z.B. ein gui-Node) — der task-Scope
        // würde beide behalten, weil der Knoten in der Task liegt. Greift der Filter ins Leere, bleiben die
        // ursprünglichen Kandidaten erhalten (besser „weiterhin mehrdeutig" als fälschlich „nicht gefunden").
        if (!string.IsNullOrEmpty(kind) && candidates.Count > 1) {
            var filtered = candidates.Where(candidate => KindMatches(candidate, kind!)).ToList();
            if (filtered.Count > 0) {
                candidates = filtered;
            }
        }

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

    // „node" ist eine grobe Sammelart (jeder Knoten); sonst exakt gegen die NavSymbolKind-Kennung
    // ("task", "gui", "init", "choice", …), wie sie auch der Kandidat meldet.
    static bool KindMatches(ISymbol symbol, string kind) =>
        string.Equals(kind, "node", StringComparison.OrdinalIgnoreCase)
            ? symbol is INodeSymbol
            : string.Equals(NavSymbolKind.Of(symbol), kind, StringComparison.OrdinalIgnoreCase);

    public static string NotFoundMessage(string name, string path) =>
        $"No task or node named '{name}' found in {path}.";

    public static string AmbiguousMessage(string name) =>
        $"The name '{name}' is ambiguous (e.g. a task and a node share the same name, or the same node name " +
        "exists in several tasks). Pass 'kind' to pick by symbol kind ('task' vs. 'node', or a specific kind " +
        "like 'gui'), and/or 'task' to scope a node to one task definition. Each candidate reports its 'kind' " +
        "and containing 'task'.";
}
