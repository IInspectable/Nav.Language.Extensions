#region Using Directives

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ModelContextProtocol.Server;

using Pharmatechnik.Nav.Language.Symbols;

#endregion

namespace Pharmatechnik.Nav.Language.Mcp.Tools;

/// <summary>
/// MCP-Tool <c>nav_find_symbol</c>: solution-weite Präfix-Suche nach Task-/Knoten-<b>Definitionen</b>. Der
/// Einstieg, wenn der Agent nur einen Namen kennt, aber nicht die Datei — die übrigen name-basierten Tools
/// (<c>nav_goto</c>, <c>nav_references</c>, …) brauchen den Pfad bereits.
/// </summary>
[McpServerToolType]
public sealed class NavFindSymbolTool {

    /// <summary>Voreinstellung für die Seitengröße, falls der Aufrufer keine angibt.</summary>
    const int DefaultLimit = 100;

    /// <summary>
    /// Obergrenze für die Seitengröße — so gewählt, dass selbst eine voll gefüllte Seite (jeder Treffer trägt
    /// einen Dateipfad, ~150 Zeichen) sicher unter dem Tool-Result-Token-Limit (~25k Tokens) bleibt.
    /// </summary>
    const int MaxLimit = 200;

    [McpServerTool(Name = "nav_find_symbol")]
    [Description("Finds task and node DEFINITIONS across the whole workspace by name prefix (case-insensitive), " +
                 "WITHOUT needing to know the file first. This is the entry point when you only have a name: use " +
                 "it to locate where e.g. 'Login' is defined, then pass a returned file path to nav_goto, " +
                 "nav_references or nav_outline to drill down. Returns each match's name, kind, containing task " +
                 "and 1-based file/line/column. Only definitions are returned (taskref imports are not — use " +
                 "nav_references for usages). Results are paged (default 100, max 200); 'truncated' = true means " +
                 "there are more — tighten the prefix or page with 'offset'. 'matchCount' is the total before paging.")]
    public static async Task<NavFindSymbolResult> FindSymbol(
        NavMcpWorkspace workspace,
        [Description("Name prefix to match (case-insensitive). Pass a full name for an exact match, or a short " +
                     "prefix to discover related symbols. An empty prefix matches all definitions (paged).")]
        string prefix,
        [Description("Optional kind filter: 'task', 'node' (any node), or a specific kind such as 'gui', " +
                     "'choice', 'init', 'exit', 'end', 'tasknode'.")]
        string? kind = null,
        [Description("Max number of definitions to return (default 100, capped at 200). Combine with 'offset' to page.")]
        int limit = DefaultLimit,
        [Description("Number of (filtered) matches to skip before returning — for paging.")]
        int offset = 0,
        CancellationToken cancellationToken = default) {

        // MCP-Parameter kommt aus der JSON-Deserialisierung — optimistische non-null-Annotation.
        // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
        var result = new NavFindSymbolResult { Prefix = prefix ?? "" };

        // Solution-weite Suche braucht die geladene Solution.
        await workspace.EnsureSolutionLoadedAsync(cancellationToken);

        // Über alle Units der Solution iterieren (derselbe Iterator wie die Referenzsuche) und je Unit die
        // Definitionen mit passendem Präfix einsammeln.
        var found = new List<ISymbol>();
        await workspace.Solution.ProcessCodeGenerationUnitsAsync(
            unit => {
                found.AddRange(NavSymbolSearch.FindDefinitionsByPrefix(unit, prefix ?? ""));
                return Task.CompletedTask;
            },
            startingUnit: null,
            cancellationToken);

        // Optionaler Art-Filter, solution-weite Dedup über (Datei, Startoffset), stabile Sortierung.
        var seen = new HashSet<(string?, int)>();
        var matched = found
                     .Where(symbol => string.IsNullOrEmpty(kind) || NavNameResolution.KindMatches(symbol, kind))
                     .Where(symbol => seen.Add((symbol.Location.FilePath, symbol.Location.Start)))
                     .OrderBy(symbol => symbol.Location.FilePath, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(symbol => symbol.Location.Start)
                     .ToList();

        var safeOffset = Math.Max(0, offset);
        var safeLimit  = limit <= 0 ? DefaultLimit : Math.Min(limit, MaxLimit);

        var page = matched.Skip(safeOffset).Take(safeLimit).ToList();

        result.MatchCount = matched.Count;
        result.Returned   = page.Count;
        result.Offset     = safeOffset;
        result.Limit      = safeLimit;
        result.Truncated  = safeOffset + page.Count < matched.Count;
        result.Symbols    = page.Select(NavSymbolRef.From).ToList();
        return result;
    }
}
