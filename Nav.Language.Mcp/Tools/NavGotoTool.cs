#region Using Directives

using System.ComponentModel;
using System.Linq;

using ModelContextProtocol.Server;

using Pharmatechnik.Nav.Language.GoTo;

#endregion

namespace Pharmatechnik.Nav.Language.Mcp.Tools;

/// <summary>
/// MCP-Tool <c>nav_goto</c>: liefert die Definition(en) eines Task- oder Knotennamens (Nav→Nav, cross-file).
/// </summary>
[McpServerToolType]
public sealed class NavGotoTool {

    [McpServerTool(Name = "nav_goto")]
    [Description("Resolves a task or node name to its Nav definition location(s), following cross-file " +
                 "includes (taskref). Returns 1-based file/line/column. If the name is ambiguous (e.g. a node " +
                 "name used in several tasks, or a task and a node sharing a name), returns candidates instead " +
                 "— pass 'kind' and/or 'task' to disambiguate. Use nav_outline first to learn the available names.")]
    public static NavGotoResult Goto(
        NavMcpWorkspace workspace,
        [Description("Absolute path to the .nav file the name lives in.")]
        string path,
        [Description("Name of the task or node to locate.")]
        string name,
        [Description("Optional task name to scope a node lookup (disambiguation).")]
        string? task = null,
        [Description("Optional symbol kind to disambiguate when a task and a node share the same name: " +
                     "'task' vs. 'node', or a specific kind like 'gui'. See the candidates' 'kind' values.")]
        string? kind = null) {

        var result = new NavGotoResult { Path = path, Name = name };

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

        // Den Offset des aufgelösten Symbols in den positions-basierten GoTo-Kern speisen — als läge der Caret
        // dort. Liefert er nichts (das Symbol IST bereits die Definition), auf die eigene Location zurückfallen.
        var targets = NavGoToService.GetGoToLocations(unit, symbol!.Location.Start);
        var locations = targets.Count > 0 ? targets : new[] { symbol.Location };

        result.Locations = locations.Select(location => NavLocationDto.From(location)).ToList();
        return result;
    }
}
