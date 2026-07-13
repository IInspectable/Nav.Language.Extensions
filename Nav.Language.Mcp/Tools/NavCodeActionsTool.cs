#region Using Directives

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;

using ModelContextProtocol.Server;

using Pharmatechnik.Nav.Language.CodeActions;
using Pharmatechnik.Nav.Language.CodeFixes;
using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.Mcp.Tools;

/// <summary>
/// MCP-Tool <c>nav_code_actions</c>: liefert die an einem Symbol anwendbaren Code-Aktionen samt Edits (read-only).
/// </summary>
[McpServerToolType]
public static class NavCodeActionsTool {

    [McpServerTool(Name = "nav_code_actions")]
    [Description("Returns the code actions (quick fixes / refactorings) applicable in a .nav file, each with its " +
                 "ready-to-apply edits (1-based ranges + new text) plus the complete file text after applying "   +
                 "just that action. Does NOT modify any file — pick one and overwrite the file with its "         +
                 "'resultText' (or apply its edits). Examples: remove an unused node/task/include, add a missing " +
                 "exit transition, introduce a choice. Pass 'name' to target a single task or node; OMIT 'name' "  +
                 "to get every action available anywhere in the file. If the name is ambiguous, returns "          +
                 "candidates — pass 'kind' and/or 'task' to disambiguate.")]
    public static NavCodeActionsResult CodeActions(
        NavMcpWorkspace workspace,
        [Description("Absolute path to the .nav file.")]
        string path,
        [Description("Optional name of the task or node the action should target. Omit to return all actions " +
                     "applicable anywhere in the file.")]
        string? name = null,
        [Description("Optional task name to scope a node lookup (disambiguation).")]
        string? task = null,
        [Description("Optional symbol kind to disambiguate when a task and a node share the same name: " +
                     "'task' vs. 'node', or a specific kind like 'gui'. See the candidates' 'kind' values.")]
        string? kind = null,
        [Description("Whether to include the complete file text after applying each action as its 'resultText'. " +
                     "Set to false to keep the result small for large files (edits only).")]
        bool includeResultText = true,
        CancellationToken cancellationToken = default) {

        var result = new NavCodeActionsResult { Path = path, Name = name ?? "" };

        var unit = workspace.GetFreshUnit(path, out _);
        if (unit == null) {
            result.Error = NavOutlineResult.NotFound(path).Error;
            return result;
        }

        var sourceText = unit.Syntax.SyntaxTree.SourceText;
        var settings   = NavEditorSettings.For(sourceText);

        TextExtent range;
        if (string.IsNullOrWhiteSpace(name)) {

            // Ohne Symbolname: der gesamte Datei-Extent — die CodeFix-Provider liefern alle Aktionen, die
            // irgendwo in der Datei anwendbar sind (ihr Symbol-/Token-Indexer greift alle vollständig
            // enthaltenen Elemente, hier also die ganze Datei).
            range = TextExtent.FromBounds(0, sourceText.Length);
        } else {

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

            // Der Bereich ist der Extent des aufgelösten Symbols — die CodeFix-Provider greifen darauf wie
            // auf eine Selektion des Bezeichners.
            range = symbol!.Location.Extent;
        }

        var actions = NavCodeActionService.GetCodeActions(unit, range, settings, cancellationToken);

        var writer = new TextChangeWriter();
        var seen   = new HashSet<string>();
        foreach (var action in actions) {

            var edits = NavEditDto.FromChanges(sourceText, action.TextChanges);
            if (edits.Count == 0) {
                continue;
            }

            // Echte Duplikate zusammenfassen — aber über Titel UND Edit-Signatur, nicht nur den Titel:
            // im Whole-File-Modus können zwei sachlich verschiedene Aktionen denselben Titel tragen (z.B.
            // 'Remove Unused Nodes' in zwei Tasks); eine reine Titel-Dedup (wie LSP/VS bei kleiner Selektion)
            // würde die zweite fälschlich verschlucken.
            if (!seen.Add(SignatureOf(action.Title, edits))) {
                continue;
            }

            result.Actions.Add(new NavCodeActionDto {
                Title      = action.Title,
                Category   = action.Category.ToString(),
                Kind       = action.Category == CodeFixCategory.Refactoring ? "refactor" : "quickfix",
                Edits      = edits,
                ResultText = includeResultText ? writer.ApplyTextChanges(sourceText.Text, action.TextChanges) : null
            });
        }

        return result;
    }

    /// <summary>
    /// Dedup-Schlüssel einer Aktion: Titel plus die 1-basierten Bereiche und Ersatztexte ihrer Edits. So
    /// fallen nur wirklich identische Aktionen zusammen (mehrfach vom selben Fleck geliefert), während
    /// gleichnamige, aber verschiedene Aktionen (Whole-File-Modus) erhalten bleiben.
    /// </summary>
    static string SignatureOf(string title, List<NavEditDto> edits) {
        var editSignature = string.Join("|", edits.Select(e => $"{e.Line}:{e.Column}:{e.EndLine}:{e.EndColumn}:{e.NewText}"));
        return $"{title} {editSignature}";
    }

}