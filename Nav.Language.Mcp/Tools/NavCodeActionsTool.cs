#region Using Directives

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;

using ModelContextProtocol.Server;

using Pharmatechnik.Nav.Language.CodeActions;
using Pharmatechnik.Nav.Language.CodeFixes;

#endregion

namespace Pharmatechnik.Nav.Language.Mcp.Tools;

/// <summary>
/// MCP-Tool <c>nav_code_actions</c>: liefert die an einem Symbol anwendbaren Code-Aktionen samt Edits (read-only).
/// </summary>
[McpServerToolType]
public static class NavCodeActionsTool {

    [McpServerTool(Name = "nav_code_actions")]
    [Description("Returns the code actions (quick fixes / refactorings) applicable at a task or node, each with " +
                 "its ready-to-apply edits (1-based ranges + new text). Does NOT modify any file — pick one and " +
                 "apply its edits yourself. Examples: remove an unused node/task/include, add a missing exit "    +
                 "transition, introduce a choice. If the name is ambiguous, returns candidates — pass 'task' to " +
                 "disambiguate.")]
    public static NavCodeActionsResult CodeActions(
        NavMcpWorkspace workspace,
        [Description("Absolute path to the .nav file.")]
        string path,
        [Description("Name of the task or node the action should target.")]
        string name,
        [Description("Optional task name to scope a node lookup (disambiguation).")]
        string? task = null,
        CancellationToken cancellationToken = default) {

        var result = new NavCodeActionsResult { Path = path, Name = name };

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

        // Der Bereich ist der Extent des aufgelösten Symbols — die CodeFix-Provider greifen darauf wie auf eine
        // Selektion des Bezeichners.
        var actions = NavCodeActionService.GetCodeActions(unit, symbol!.Location.Extent, settings, cancellationToken);

        var seenTitles = new HashSet<string>();
        foreach (var action in actions) {

            // Doppelte Titel zusammenfassen (wie der LSP-Server / die VS-SuggestedActionsSource).
            if (!seenTitles.Add(action.Title)) {
                continue;
            }

            var edits = NavEditDto.FromChanges(sourceText, action.TextChanges);
            if (edits.Count == 0) {
                continue;
            }

            result.Actions.Add(new NavCodeActionDto {
                Title    = action.Title,
                Category = action.Category.ToString(),
                Kind     = action.Category == CodeFixCategory.Refactoring ? "refactor" : "quickfix",
                Edits    = edits
            });
        }

        return result;
    }

}