#region Using Directives

using System.ComponentModel;
using System.Linq;

using ModelContextProtocol.Server;

using Pharmatechnik.Nav.Language.Rename;

#endregion

namespace Pharmatechnik.Nav.Language.Mcp.Tools;

/// <summary>
/// MCP-Tool <c>nav_rename</c>: berechnet das Edit-Set zum Umbenennen eines Task-/Knotennamens (read-only).
/// </summary>
[McpServerToolType]
public static class NavRenameTool {

    [McpServerTool(Name = "nav_rename")]
    [Description("Computes the edits to rename a task or node and returns them (1-based ranges + new text). "        +
                 "Does NOT modify any file — apply the returned edits yourself. The rename is file-local (same "     +
                 "behaviour as the VS rename): references in other files are not changed. An invalid new name "      +
                 "(keyword, already taken, …) is reported in 'error'. If the name is ambiguous, returns candidates " +
                 "— pass 'task' to disambiguate.")]
    public static NavRenameResult Rename(
        NavMcpWorkspace workspace,
        [Description("Absolute path to the .nav file.")]
        string path,
        [Description("Current name of the task or node to rename.")]
        string name,
        [Description("The new name.")] string newName,
        [Description("Optional task name to scope a node lookup (disambiguation).")]
        string? task = null) {

        var result = new NavRenameResult { Path = path, Name = name, NewName = newName };

        var unit = workspace.GetFreshUnit(path, out _);
        if (unit == null) {
            result.Error = NavOutlineResult.NotFound(path).Error;
            return result;
        }

        var status = NavNameResolution.Resolve(unit, name, task, out var symbol, out var candidates);

        if (status == NavNameResolution.Status.NotFound) {
            result.Error = NavNameResolution.NotFoundMessage(name, path);
            return result;
        }

        if (status == NavNameResolution.Status.Ambiguous) {
            result.Error      = NavNameResolution.AmbiguousMessage(name);
            result.Candidates = candidates.Select(NavSymbolRef.From).ToList();
            return result;
        }

        var sourceText = unit.Syntax.SyntaxTree.SourceText;
        var settings   = NavEditorSettings.For(sourceText);

        var renameFix = NavRenameService.GetRenameFix(unit, symbol!.Location.Start, settings);
        if (renameFix == null) {
            result.Error = $"The symbol '{name}' cannot be renamed.";
            return result;
        }

        var trimmedNewName    = newName?.Trim() ?? string.Empty;
        var validationMessage = renameFix.ValidateSymbolName(trimmedNewName);
        if (!string.IsNullOrEmpty(validationMessage)) {
            result.Error = validationMessage;
            return result;
        }

        result.Edits = NavEditDto.FromChanges(sourceText, renameFix.GetTextChanges(trimmedNewName));
        return result;
    }

}