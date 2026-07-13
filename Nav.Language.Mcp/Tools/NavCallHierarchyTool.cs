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
/// MCP-Tool <c>nav_call_hierarchy</c>: liefert die Aufrufbeziehungen einer Task auf Task-Ebene —
/// ausgehend (welche Tasks ruft sie auf) und/oder eingehend (welche Tasks rufen sie solution-weit auf).
/// Engine-Kern ist <see cref="NavCallHierarchyService"/>, derselbe VS-freie Kern, den die LSP-Call-Hierarchy
/// nutzt; hier **name-basiert** (der Agent hat keinen Cursor): die Task wird über ihren Namen in
/// <paramref name="path"/> aufgelöst und in den Engine-Kern gespeist.
/// </summary>
[McpServerToolType]
public static class NavCallHierarchyTool {

    const string DirectionIncoming = "incoming";
    const string DirectionOutgoing = "outgoing";
    const string DirectionBoth     = "both";

    [McpServerTool(Name = "nav_call_hierarchy")]
    [Description("Returns the task-level call relationships of a Nav task: outgoing (which tasks it calls via " +
                 "'task' nodes) and/or incoming (which tasks call it, searched across the whole workspace). The " +
                 "Nav call graph runs through 'task' nodes (task Sub Foo;) that reference a task declaration, "  +
                 "also cross-file via taskref. Use this for cross-task impact analysis — e.g. 'what breaks if I " +
                 "change this task' (incoming) or 'what does this task depend on' (outgoing). Name-based: pass "  +
                 "the task name and the file it is defined in. Returns 1-based file/line/column for the other "  +
                 "task and for each call site. 'direction' selects incoming | outgoing | both (default both).")]
    public static async Task<NavCallHierarchyResult> CallHierarchy(
        NavMcpWorkspace workspace,
        [Description("Absolute path to the .nav file the task is defined in.")]
        string path,
        [Description("Name of the task whose call relationships to return (must be a task definition in the file).")]
        string task,
        [Description("Which relationships to return: 'incoming' (callers), 'outgoing' (callees) or 'both'. " +
                     "Default 'both'.")]
        string direction = DirectionBoth,
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
            result.Outgoing = NavCallHierarchyService.GetOutgoingCalls(taskDefinition)
                                                     .Select(call => new NavCallDto {
                                                          Task      = call.Target.Name,
                                                          Location  = NavLocationDto.From(call.Target.Location),
                                                          CallSites = call.CallSites.Select(loc => NavLocationDto.From(loc)).ToList()
                                                      })
                                                     .ToList();
        }

        if (wantIncoming) {
            var incoming = await NavCallHierarchyService.GetIncomingCallsAsync(taskDefinition, workspace.Solution, cancellationToken);
            result.Incoming = incoming.Select(call => new NavCallDto {
                                           Task      = call.Caller.Name,
                                           Location  = NavLocationDto.From(call.Caller.Location),
                                           CallSites = call.CallSites.Select(loc => NavLocationDto.From(loc)).ToList()
                                       })
                                      .ToList();
        }

        return result;
    }

}
