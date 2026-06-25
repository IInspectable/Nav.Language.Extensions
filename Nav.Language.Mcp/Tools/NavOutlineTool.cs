#region Using Directives

using System.ComponentModel;

using ModelContextProtocol.Server;

#endregion

namespace Pharmatechnik.Nav.Language.Mcp.Tools;

/// <summary>
/// MCP-Tool <c>nav_outline</c>: liefert die Struktur einer <c>.nav</c>-Datei (Tasks + Knoten).
/// </summary>
[McpServerToolType]
public static class NavOutlineTool {

    [McpServerTool(Name = "nav_outline")]
    [Description("Returns the structure of a Nav (.nav) file: every task definition with its declared nodes "     +
                 "(name, kind such as init/exit/end/choice/gui, and 1-based line/column). Use this to grasp a "   +
                 "file's layout and to learn the exact task and node names to pass to nav_goto, nav_references, " +
                 "nav_rename or nav_code_actions — without reading the whole file.")]
    public static NavOutlineResult Outline(
        NavMcpWorkspace workspace,
        [Description("Absolute path to the .nav file.")]
        string path) {

        var unit = workspace.GetFreshUnit(path, out _);
        if (unit == null) {
            return NavOutlineResult.NotFound(path);
        }

        return NavOutlineResult.From(path, unit);
    }

}