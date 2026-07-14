#region Using Directives

using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ModelContextProtocol.Server;

using Pharmatechnik.Nav.Language.FindReferences;
using Pharmatechnik.Nav.Language.References;

#endregion

namespace Pharmatechnik.Nav.Language.Mcp.Tools;

/// <summary>
/// MCP-Tool <c>nav_references</c>: findet alle solution-weiten Vorkommen eines Task- oder Knotennamens.
/// </summary>
[McpServerToolType]
public sealed class NavReferencesTool {

    /// <summary>Voreinstellung für die Seitengröße, falls der Aufrufer keine angibt.</summary>
    const int DefaultLimit = 100;

    /// <summary>
    /// Obergrenze für die Seitengröße — so gewählt, dass selbst eine voll gefüllte Seite (jede Location trägt
    /// einen Dateipfad, ~150 Zeichen) sicher unter dem Tool-Result-Token-Limit (~25k Tokens) bleibt.
    /// </summary>
    const int MaxLimit = 200;

    [McpServerTool(Name = "nav_references")]
    [Description("Finds all references to a task or node across the whole workspace (solution-wide), including " +
                 "the declaration itself (marked isDeclaration). Returns 1-based file/line/column per occurrence. " +
                 "Use this before renaming or removing a symbol to see where it is used. If the name is ambiguous, " +
                 "returns candidates — pass 'kind' and/or 'task' to disambiguate. Heavily-referenced symbols are " +
                 "paged: at most 'limit' locations are returned (default 100, max 200); 'truncated' = true means " +
                 "there are more — narrow via 'filter' or page with 'offset'. 'count' is the total, 'matchCount' " +
                 "the number matching the filter.")]
    public static async Task<NavReferencesResult> References(
        NavMcpWorkspace workspace,
        [Description("Absolute path to the .nav file the name lives in.")]
        string path,
        [Description("Name of the task or node whose references to find.")]
        string name,
        [Description("Optional task name to scope a node lookup (disambiguation).")]
        string? task = null,
        [Description("Optional symbol kind to disambiguate when a task and a node share the same name: " +
                     "'task' vs. 'node', or a specific kind like 'gui'. See the candidates' 'kind' values.")]
        string? kind = null,
        [Description("Optional case-insensitive substring matched against each occurrence's file path; only " +
                     "matching locations are returned. Use it to scope a heavily-referenced symbol to a subfolder " +
                     "or file.")]
        string? filter = null,
        [Description("Max number of locations to return (default 100, capped at 200). Combine with 'offset' to page.")]
        int limit = DefaultLimit,
        [Description("Number of (filtered) locations to skip before returning — for paging.")]
        int offset = 0,
        CancellationToken cancellationToken = default) {

        var result = new NavReferencesResult { Path = path, Name = name };

        // Solution-weite Suche braucht die geladene Solution.
        await workspace.EnsureSolutionLoadedAsync(cancellationToken);

        var unit = workspace.GetFreshUnit(path, out _);
        if (unit == null) {
            result.Error = NavOutlineResult.NotFound(path).Error;
            return result;
        }

        var status = NavNameResolution.Resolve(unit, name, task, kind, out var symbol, out var candidates);

        if (status == NavNameResolution.Status.NotFound) {
            result.Error = NavNameResolution.NotFoundMessage(name, path);
            return result;
        }

        if (status == NavNameResolution.Status.Ambiguous) {
            result.Error      = NavNameResolution.AmbiguousMessage(name);
            result.Candidates = candidates.Select(NavSymbolRef.From).ToList();
            return result;
        }

        // Exakt der LSP-Pfad: das Symbol an der Position als Ursprung, dann die Engine-Referenzsuche.
        var origin = NavReferenceService.FindSymbol(unit, symbol!.Location.Start);
        if (origin == null) {
            result.Error = NavNameResolution.NotFoundMessage(name, path);
            return result;
        }

        var collector = new McpReferenceCollector(includeDeclaration: true, cancellationToken);
        var args      = new FindReferencesArgs(origin, unit, workspace.Solution, collector);

        await ReferenceFinder.FindReferencesAsync(args);

        var all = collector.Results
                           .Select(item => NavLocationDto.From(item.Location, item.IsDeclaration))
                           .ToList();

        var matched = all.Where(loc => string.IsNullOrEmpty(filter) ||
                                       loc.File.Contains(filter, StringComparison.OrdinalIgnoreCase))
                         .ToList();

        var safeOffset = Math.Max(0, offset);
        var safeLimit  = limit <= 0 ? DefaultLimit : Math.Min(limit, MaxLimit);

        var page = matched.Skip(safeOffset).Take(safeLimit).ToList();

        result.Count      = all.Count;
        result.MatchCount = matched.Count;
        result.Returned   = page.Count;
        result.Offset     = safeOffset;
        result.Limit      = safeLimit;
        result.Truncated  = safeOffset + page.Count < matched.Count;
        result.Locations  = page;
        return result;
    }
}
