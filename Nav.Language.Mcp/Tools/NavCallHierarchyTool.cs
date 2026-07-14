#region Using Directives

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ModelContextProtocol.Server;

using Pharmatechnik.Nav.Language.CallHierarchy;

#endregion

namespace Pharmatechnik.Nav.Language.Mcp.Tools;

/// <summary>
/// MCP-Tool <c>nav_call_hierarchy</c>: liefert die Aufrufbeziehungen einer Task auf Task-Ebene —
/// ausgehend (welche Tasks ruft sie auf) und/oder eingehend (welche Tasks rufen sie solution-weit auf).
/// Engine-Kern ist <see cref="NavCallHierarchyService"/>, derselbe VS-freie Kern, den die LSP-Call-Hierarchy
/// nutzt; hier **name-basiert** (der Agent hat keinen Cursor): die Task wird über ihren Namen in
/// <paramref name="path"/> aufgelöst und in den Engine-Kern gespeist.
/// </summary>
[McpServerToolType]
public sealed class NavCallHierarchyTool {

    const string DirectionIncoming = "incoming";
    const string DirectionOutgoing = "outgoing";
    const string DirectionBoth     = "both";

    const string DetailSummary = "summary";
    const string DetailFull    = "full";

    /// <summary>Voreinstellung für die Seitengröße der eingehenden Aufrufer, falls der Aufrufer keine angibt.</summary>
    const int DefaultLimit = 100;

    /// <summary>
    /// Obergrenze für die Seitengröße — analog <see cref="NavReferencesTool"/> so gewählt, dass selbst eine
    /// voll gefüllte Seite (jeder Aufrufer trägt einen Dateipfad, ~150 Zeichen) sicher unter dem
    /// Tool-Result-Token-Limit (~25k Tokens) bleibt.
    /// </summary>
    const int MaxLimit = 200;

    [McpServerTool(Name = "nav_call_hierarchy")]
    [Description("Returns the task-level call relationships of a Nav task: outgoing (which tasks it calls via " +
                 "'task' nodes) and/or incoming (which tasks call it, searched across the whole workspace). The " +
                 "Nav call graph runs through 'task' nodes (task Sub Foo;) that reference a task declaration, "  +
                 "also cross-file via taskref. Use this for cross-task impact analysis — e.g. 'what breaks if I " +
                 "change this task' (incoming) or 'what does this task depend on' (outgoing). Name-based: pass "  +
                 "the task name and the file it is defined in. Returns 1-based file/line/column for the other "  +
                 "task; 'callerCount'/'calleeCount'/'callSiteCount' give the totals. By default ('detail' = "    +
                 "'summary') each relationship carries only the other task, its definition location and a "      +
                 "'callSiteCount' — enough for 'who calls X'; pass detail='full' to also get every individual "  +
                 "call-site position. Incoming callers (potentially many) are filtered/paged: 'filter' scopes "  +
                 "by caller file path, at most 'limit' callers are returned (default 100, max 200), 'truncated' " +
                 "= true means there are more (raise 'offset' or narrow 'filter'). 'direction' selects incoming " +
                 "| outgoing | both (default both).")]
    public static async Task<NavCallHierarchyResult> CallHierarchy(
        NavMcpWorkspace workspace,
        [Description("Absolute path to the .nav file the task is defined in.")]
        string path,
        [Description("Name of the task whose call relationships to return (must be a task definition in the file).")]
        string task,
        [Description("Which relationships to return: 'incoming' (callers), 'outgoing' (callees) or 'both'. " +
                     "Default 'both'.")]
        string direction = DirectionBoth,
        [Description("Level of detail: 'summary' (default) returns per relationship only the task, its location " +
                     "and 'callSiteCount'; 'full' additionally lists every call-site position.")]
        string detail = DetailSummary,
        [Description("Optional case-insensitive substring matched against each incoming caller's file path; only " +
                     "matching callers are returned. Use it to scope a heavily-called task to a module/subfolder.")]
        string? filter = null,
        [Description("Max number of incoming callers to return (default 100, capped at 200). Combine with " +
                     "'offset' to page. Does not affect outgoing calls (always complete).")]
        int limit = DefaultLimit,
        [Description("Number of (filtered) incoming callers to skip before returning — for paging.")]
        int offset = 0,
        CancellationToken cancellationToken = default) {

        var normalizedDirection = (direction ?? DirectionBoth).Trim().ToLowerInvariant();

        var result = new NavCallHierarchyResult {
            Path      = path,
            Task      = task,
            Direction = normalizedDirection
        };

        if (normalizedDirection != DirectionIncoming &&
            normalizedDirection != DirectionOutgoing &&
            normalizedDirection != DirectionBoth) {
            result.Error = $"Invalid 'direction' '{direction}'. Use 'incoming', 'outgoing' or 'both'.";
            return result;
        }

        var normalizedDetail = (detail ?? DetailSummary).Trim().ToLowerInvariant();
        if (normalizedDetail != DetailSummary && normalizedDetail != DetailFull) {
            result.Error = $"Invalid 'detail' '{detail}'. Use 'summary' or 'full'.";
            return result;
        }

        var full         = normalizedDetail == DetailFull;
        var wantIncoming = normalizedDirection is DirectionIncoming or DirectionBoth;
        var wantOutgoing = normalizedDirection is DirectionOutgoing or DirectionBoth;

        // Eingehende Aufrufe scannen die ganze Solution — die muss geladen sein.
        if (wantIncoming) {
            await workspace.EnsureSolutionLoadedAsync(cancellationToken);
        }

        var unit = workspace.GetFreshUnit(path, out _);
        if (unit == null) {
            result.Error = NavCallHierarchyResult.NotFound(path).Error;
            return result;
        }

        // Die Call-Hierarchy ist strikt Task-Ebene: die Ausgangs-Task muss eine Definition in dieser Datei
        // sein (Task-Namen sind je Datei eindeutig). Ein Knotenname o.ä. wird hier bewusst nicht aufgelöst.
        var taskDefinition = unit.TaskDefinitions.FirstOrDefault(t => String.Equals(t.Name, task, StringComparison.Ordinal));
        if (taskDefinition == null) {
            result.Error = $"No task named '{task}' is defined in {path}. " +
                           "Pass a task definition name (use nav_find_symbol/nav_outline to locate it).";
            return result;
        }

        if (wantOutgoing) {
            // Ausgehend ist durch den Task-Rumpf begrenzt → stets vollständig, nur der Detailgrad steuert die Call-Sites.
            result.Outgoing = NavCallHierarchyService.GetOutgoingCalls(taskDefinition)
                                                     .Select(call => ToDto(call.Target.Name, call.Target.Location, call.CallSites, full))
                                                     .ToList();
            result.CalleeCount = result.Outgoing.Count;
        }

        if (wantIncoming) {
            var incoming = await NavCallHierarchyService.GetIncomingCallsAsync(taskDefinition, workspace.Solution, cancellationToken);

            // Pro Aufrufer die (bereits materialisierten) Call-Sites festhalten (für Count + optionale Detailausgabe).
            var groups = incoming.Select(call => new {
                                      call.Caller,
                                      call.CallSites
                                  })
                                 .ToList();

            result.CallerCount   = groups.Count;
            result.CallSiteCount = groups.Sum(g => g.CallSites.Count);

            var matched = groups.Where(g => string.IsNullOrEmpty(filter) ||
                                            (g.Caller.Location.FilePath ?? "").Contains(filter, StringComparison.OrdinalIgnoreCase))
                                .ToList();

            var safeOffset = Math.Max(0, offset);
            var safeLimit  = limit <= 0 ? DefaultLimit : Math.Min(limit, MaxLimit);
            var page       = matched.Skip(safeOffset).Take(safeLimit).ToList();

            result.MatchCount = matched.Count;
            result.Offset     = safeOffset;
            result.Limit      = safeLimit;
            result.Returned   = page.Count;
            result.Truncated  = safeOffset + page.Count < matched.Count;
            result.Incoming   = page.Select(g => ToDto(g.Caller.Name, g.Caller.Location, g.CallSites, full)).ToList();
        }

        return result;
    }

    /// <summary>
    /// Baut einen <see cref="NavCallDto"/>: Name + Definitions-Location der anderen Task, die Call-Site-Anzahl
    /// und — nur bei <paramref name="full"/> — die einzelnen Call-Site-Positionen (kompakt, ohne Dateipfad).
    /// </summary>
    static NavCallDto ToDto(string otherTask, Location location, IReadOnlyList<Location> callSites, bool full) {

        return new NavCallDto {
            Task          = otherTask,
            Location      = NavLocationDto.From(location),
            CallSiteCount = callSites.Count,
            CallSites     = full ? callSites.Select(NavPositionDto.From).ToList() : new List<NavPositionDto>()
        };
    }

}
