#region Using Directives

using System.ComponentModel;

using ModelContextProtocol.Server;

#endregion

namespace Pharmatechnik.Nav.Language.Mcp.Tools;

/// <summary>
/// MCP-Tool-Oberfläche für Nav. <see cref="NavMcpWorkspace"/> wird per DI injiziert (als Singleton
/// registriert), der <c>path</c>-Parameter stammt aus den Tool-Argumenten des Clients.
/// </summary>
[McpServerToolType]
public static class NavValidateTool {

    [McpServerTool(Name = "nav_validate")]
    [Description("Validates a Nav (.nav) file and returns all diagnostics (errors, warnings, suggestions), " +
                 "including cross-file diagnostics caused by included files. Call this right after editing a " +
                 ".nav file to confirm the change did not break anything. Returns counts per severity and a " +
                 "list of diagnostics with 1-based line/column positions.")]
    public static NavValidateResult Validate(
        NavMcpWorkspace workspace,
        [Description("Absolute path to the .nav file to validate.")] string path) {

        return workspace.Validate(path);
    }
}
