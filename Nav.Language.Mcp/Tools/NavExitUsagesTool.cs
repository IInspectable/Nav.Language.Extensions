#region Using Directives

using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ModelContextProtocol.Server;

using Pharmatechnik.Nav.Language.CallHierarchy;

#endregion

namespace Pharmatechnik.Nav.Language.Mcp.Tools;

/// <summary>
/// MCP-Tool <c>nav_exit_usages</c>: liefert solution-weit alle Stellen, an denen ein Exit einer Task über
/// eine Instanz benutzt wird — die <c>Instanz:&lt;exit&gt; --&gt; …</c>-Kanten in den aufrufenden Tasks. Das
/// ist der echte Rename-Blast-Radius eines Exits, den <c>nav_references</c> NICHT liefert. Engine-Kern ist
/// <see cref="NavCallHierarchyService.GetExitUsagesAsync"/>; hier **name-basiert** (der Agent hat keinen
/// Cursor): die Task wird über ihren Namen in <paramref name="path"/> aufgelöst.
/// </summary>
[McpServerToolType]
public static class NavExitUsagesTool {

    /// <summary>Voreinstellung für die Seitengröße der aufrufenden Tasks, falls der Aufrufer keine angibt.</summary>
    const int DefaultLimit = 100;

    /// <summary>
    /// Obergrenze für die Seitengröße — analog <see cref="NavCallHierarchyTool"/> so gewählt, dass selbst eine
    /// voll gefüllte Seite sicher unter dem Tool-Result-Token-Limit (~25k Tokens) bleibt.
    /// </summary>
    const int MaxLimit = 200;

    [McpServerTool(Name = "nav_exit_usages")]
    [Description("Finds every place a task's exit is used from an instance across the whole workspace — the "     +
                 "'Instance:<exit> --> …' edges in calling tasks. This is the true rename blast radius of an "    +
                 "exit: nav_references only reports the exit's local incoming edges and MISSES these cross-file " +
                 "instance edges, so use this before renaming or removing an exit. Pass the task and the file "   +
                 "it is defined in; optionally 'exit' to scope to a single exit name (omit for all exits of the " +
                 "task). Returns 1-based positions grouped by calling task; 'callerCount'/'siteCount' give the "  +
                 "totals. Callers (potentially many) are filtered/paged: 'filter' scopes by caller file path, "   +
                 "at most 'limit' callers are returned (default 100, max 200), 'truncated' = true means there "   +
                 "are more (raise 'offset' or narrow 'filter').")]
    public static async Task<NavExitUsagesResult> ExitUsages(
        NavMcpWorkspace workspace,
        [Description("Absolute path to the .nav file the task is defined in.")]
        string path,
        [Description("Name of the task whose exit usages to return (must be a task definition in the file).")]
        string task,
        [Description("Optional exit name to scope to a single exit (e.g. 'AccessDenied'). Omit to return the " +
                     "usages of all exits of the task.")]
        string? exit = null,
        [Description("Optional case-insensitive substring matched against each calling task's file path; only " +
                     "matching callers are returned. Use it to scope a heavily-used exit to a module/subfolder.")]
        string? filter = null,
        [Description("Max number of calling tasks to return (default 100, capped at 200). Combine with 'offset' to page.")]
        int limit = DefaultLimit,
        [Description("Number of (filtered) calling tasks to skip before returning — for paging.")]
        int offset = 0,
        CancellationToken cancellationToken = default) {

        var result = new NavExitUsagesResult {
            Path = path,
            Task = task,
            Exit = exit ?? ""
        };

        // Exit-Nutzungen scannen die ganze Solution — die muss geladen sein.
        await workspace.EnsureSolutionLoadedAsync(cancellationToken);

        var unit = workspace.GetFreshUnit(path, out _);
        if (unit == null) {
            result.Error = NavExitUsagesResult.NotFound(path).Error;
            return result;
        }

        // Strikt Task-Ebene: die Ausgangs-Task muss eine Definition in dieser Datei sein (Task-Namen sind je
        // Datei eindeutig) — analog nav_call_hierarchy.
        var taskDefinition = unit.TaskDefinitions.FirstOrDefault(t => String.Equals(t.Name, task, StringComparison.Ordinal));
        if (taskDefinition == null) {
            result.Error = $"No task named '{task}' is defined in {path}. " +
                           "Pass a task definition name (use nav_find_symbol/nav_outline to locate it).";
            return result;
        }

        var usages = await NavCallHierarchyService.GetExitUsagesAsync(taskDefinition, exit, workspace.Solution, cancellationToken);

        result.CallerCount = usages.Count;
        result.SiteCount   = usages.Sum(u => u.Sites.Count);

        var matched = usages.Where(u => string.IsNullOrEmpty(filter) ||
                                        (u.Caller.Location.FilePath ?? "").Contains(filter, StringComparison.OrdinalIgnoreCase))
                            .ToList();

        var safeOffset = Math.Max(0, offset);
        var safeLimit  = limit <= 0 ? DefaultLimit : Math.Min(limit, MaxLimit);
        var page       = matched.Skip(safeOffset).Take(safeLimit).ToList();

        result.MatchCount = matched.Count;
        result.Offset     = safeOffset;
        result.Limit      = safeLimit;
        result.Returned   = page.Count;
        result.Truncated  = safeOffset + page.Count < matched.Count;
        result.Usages     = page.Select(ToDto).ToList();

        return result;
    }

    /// <summary>Baut einen <see cref="NavExitUsageDto"/>: aufrufende Task, deren Definitions-Position und ihre Exit-Kanten.</summary>
    static NavExitUsageDto ToDto(ExitConnectionPointUsage usage) {

        return new NavExitUsageDto {
            Caller    = usage.Caller.Name,
            Location  = NavLocationDto.From(usage.Caller.Location),
            SiteCount = usage.Sites.Count,
            Sites = usage.Sites.Select(site => new NavExitEdgeDto {
                              Exit     = site.ExitName,
                              Instance = site.InstanceName,
                              Position = NavPositionDto.From(site.Location)
                          })
                         .ToList()
        };
    }

}
