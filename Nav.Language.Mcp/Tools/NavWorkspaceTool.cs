#region Using Directives

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

    [McpServerTool(Name = "nav_workspace")]
    [Description("Lists all Nav (.nav) files in the workspace (recursively below the workspace root), with " +
                 "their relative and absolute paths. Use this to discover the project's .nav files and to get " +
                 "absolute paths to pass to the other nav_* tools.")]
    public static async Task<NavWorkspaceResult> Workspace(
        NavMcpWorkspace workspace,
        CancellationToken cancellationToken = default) {

        await workspace.EnsureSolutionLoadedAsync(cancellationToken);

        var root = workspace.SolutionDirectory?.FullName;

        var files = workspace.Solution.SolutionFiles
                             .Select(file => new NavFileEntry {
                                  Path         = file.FullName,
                                  RelativePath = root != null ? PathHelper.GetRelativePath(root, file.FullName) : file.FullName
                              })
                             .OrderBy(entry => entry.RelativePath, System.StringComparer.OrdinalIgnoreCase)
                             .ToList();

        return new NavWorkspaceResult {
            Root      = root,
            FileCount = workspace.FileCount,
            Files     = files
        };
    }
}
