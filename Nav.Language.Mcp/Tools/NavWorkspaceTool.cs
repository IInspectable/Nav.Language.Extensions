#region Using Directives

using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ModelContextProtocol.Server;

using Pharmatechnik.Nav.Utilities.IO;

#endregion

namespace Pharmatechnik.Nav.Language.Mcp.Tools;

/// <summary>
/// MCP-Tool <c>nav_workspace</c>: listet alle <c>.nav</c>-Dateien der Solution.
/// </summary>
[McpServerToolType]
public static class NavWorkspaceTool {

    /// <summary>Voreinstellung für die Seitengröße, falls der Aufrufer keine angibt.</summary>
    const int DefaultLimit = 100;

    /// <summary>
    /// Obergrenze für die Seitengröße — so gewählt, dass selbst eine voll gefüllte Seite (jeder Eintrag trägt
    /// relativen + absoluten Pfad, ~240 Zeichen) sicher unter dem Tool-Result-Token-Limit (~25k Tokens) bleibt.
    /// </summary>
    const int MaxLimit = 200;

    [McpServerTool(Name = "nav_workspace")]
    [Description("Lists Nav (.nav) files in the workspace (recursively below the workspace root), with their " +
                 "relative and absolute paths. Use this to discover the project's .nav files and to get absolute " +
                 "paths to pass to the other nav_* tools. Large workspaces are paged: at most 'limit' files are " +
                 "returned (default 100, max 200); 'truncated' = true means there are more — narrow via 'filter' " +
                 "or page with 'offset'. 'fileCount' is the total, 'matchCount' the number matching the filter.")]
    public static async Task<NavWorkspaceResult> Workspace(
        NavMcpWorkspace workspace,
        [Description("Optional case-insensitive substring matched against the relative path; only matching files " +
                     "are returned. Use it to narrow large workspaces (a subfolder or a name fragment).")]
        string? filter = null,
        [Description("Max number of files to return (default 100, capped at 200). Combine with 'offset' to page.")]
        int limit = DefaultLimit,
        [Description("Number of (filtered) files to skip before returning — for paging.")]
        int offset = 0,
        CancellationToken cancellationToken = default) {

        await workspace.EnsureSolutionLoadedAsync(cancellationToken);

        var root = workspace.SolutionDirectory?.FullName;

        var matched = workspace.Solution.SolutionFiles
                               .Select(file => new NavFileEntry {
                                    Path         = file.FullName,
                                    RelativePath = root != null ? PathHelper.GetRelativePath(root, file.FullName) : file.FullName
                                })
                               .Where(entry => string.IsNullOrEmpty(filter) ||
                                               entry.RelativePath.Contains(filter, StringComparison.OrdinalIgnoreCase))
                               .OrderBy(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase)
                               .ToList();

        var safeOffset = Math.Max(0, offset);
        var safeLimit  = limit <= 0 ? DefaultLimit : Math.Min(limit, MaxLimit);

        var page = matched.Skip(safeOffset).Take(safeLimit).ToList();

        return new NavWorkspaceResult {
            Root       = root,
            FileCount  = workspace.FileCount,
            MatchCount = matched.Count,
            Returned   = page.Count,
            Offset     = safeOffset,
            Limit      = safeLimit,
            Truncated  = safeOffset + page.Count < matched.Count,
            Files      = page
        };
    }
}
