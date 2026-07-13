#region Using Directives

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ModelContextProtocol.Server;

using Pharmatechnik.Nav.Language.CallHierarchy;
using Pharmatechnik.Nav.Language.FindReferences;
using Pharmatechnik.Nav.Language.References;
using Pharmatechnik.Nav.Language.Rename;
using Pharmatechnik.Nav.Language.Text;
using Pharmatechnik.Nav.Utilities.IO;

#endregion

namespace Pharmatechnik.Nav.Language.Mcp.Tools;

/// <summary>
/// MCP-Tool <c>nav_rename</c>: berechnet das Edit-Set zum Umbenennen eines Task-/Knotennamens (read-only).
/// </summary>
[McpServerToolType]
public static class NavRenameTool {

    [McpServerTool(Name = "nav_rename")]
    [Description("Computes the edits to rename a task or node and returns them (1-based ranges + new text) plus "    +
                 "the complete file text after applying them. Does NOT modify any file — apply the result "         +
                 "yourself (easiest: overwrite the file with 'resultText'). The rename is file-local (same "        +
                 "behaviour as the VS rename): references in OTHER files are not changed. When the renamed symbol " +
                 "is visible across files (a task name, or an exit used from an instance as 'Instance:<exit>'), "   +
                 "those other files would silently break (NAV0010) — they are reported in 'warning'/"              +
                 "'crossFileFiles'/'crossFileReferenceCount' so you can re-run the rename per file (see "           +
                 "nav_references/nav_exit_usages). An invalid new name (keyword, already taken, …) is reported in " +
                 "'error'. If the name is ambiguous, returns candidates — pass 'kind' and/or 'task' to disambiguate.")]
    public static async Task<NavRenameResult> Rename(
        NavMcpWorkspace workspace,
        [Description("Absolute path to the .nav file.")]
        string path,
        [Description("Current name of the task or node to rename.")]
        string name,
        [Description("The new name.")] string newName,
        [Description("Optional task name to scope a node lookup (disambiguation).")]
        string? task = null,
        [Description("Optional symbol kind to disambiguate when a task and a node share the same name: " +
                     "'task' vs. 'node', or a specific kind like 'gui'. See the candidates' 'kind' values.")]
        string? kind = null,
        [Description("Whether to include the complete file text after the rename as 'resultText'. Set to " +
                     "false to keep the result small for large files (edits only).")]
        bool includeResultText = true,
        CancellationToken cancellationToken = default) {

        var result = new NavRenameResult { Path = path, Name = name, NewName = newName };

        var unit = workspace.GetFreshUnit(path, out var normalizedPath);
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

        var sourceText = unit.Syntax.SyntaxTree.SourceText;
        var settings   = NavEditorSettings.For(sourceText);

        var renameFix = NavRenameService.GetRenameFix(unit, symbol!.Location.Start, settings);
        if (renameFix == null) {
            result.Error = $"The symbol '{name}' cannot be renamed.";
            return result;
        }

        // MCP-Parameter kommt aus der JSON-Deserialisierung — optimistische non-null-Annotation.
        // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
        var trimmedNewName    = newName?.Trim() ?? string.Empty;
        var validationMessage = renameFix.ValidateSymbolName(trimmedNewName);
        if (!string.IsNullOrEmpty(validationMessage)) {
            result.Error = validationMessage;
            return result;
        }

        var changes = renameFix.GetTextChanges(trimmedNewName).ToList();
        result.Edits = NavEditDto.FromChanges(sourceText, changes);

        if (includeResultText && changes.Count > 0) {
            result.ResultText = new TextChangeWriter().ApplyTextChanges(sourceText.Text, changes);
        }

        await PopulateCrossFileWarningAsync(workspace, unit, symbol!, normalizedPath, result, cancellationToken);

        return result;
    }

    /// <summary>
    /// Der Rename ist dateilokal (wie der VS-Rename), aber manche Symbole sind über Dateigrenzen sichtbar —
    /// dann brechen die Referenzen in anderen Dateien still (NAV0010). Diese Dateien ermitteln und als
    /// <see cref="NavRenameResult.Warning"/>/<see cref="NavRenameResult.CrossFileFiles"/> ausweisen; die Edits
    /// selbst bleiben unverändert dateilokal.
    /// </summary>
    static async Task PopulateCrossFileWarningAsync(NavMcpWorkspace workspace,
                                                    CodeGenerationUnit unit,
                                                    ISymbol symbol,
                                                    string normalizedPath,
                                                    NavRenameResult result,
                                                    CancellationToken cancellationToken) {

        await workspace.EnsureSolutionLoadedAsync(cancellationToken);

        var seen           = new HashSet<(string?, int)>();
        var crossFileFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var crossFileCount = 0;

        void Collect(Location? location) {
            if (location?.FilePath == null) {
                return;
            }

            var normalized = PathHelper.NormalizePath(location.FilePath) ?? location.FilePath;
            if (string.Equals(normalized, normalizedPath, StringComparison.OrdinalIgnoreCase)) {
                return; // dateilokal — vom Rename bereits abgedeckt.
            }

            if (seen.Add((normalized, location.Start))) {
                crossFileCount++;
                crossFileFiles.Add(normalized);
            }
        }

        // 1) Solution-weite Referenzsuche (exakt der nav_references-Pfad): findet u.a. die task-Knoten, die
        //    einen umzubenennenden Task cross-file referenzieren.
        var origin = NavReferenceService.FindSymbol(unit, symbol.Location.Start);
        if (origin != null) {
            var collector = new McpReferenceCollector(includeDeclaration: false, cancellationToken);
            await ReferenceFinder.FindReferencesAsync(new FindReferencesArgs(origin, unit, workspace.Solution, collector));

            foreach (var (location, _) in collector.Results) {
                Collect(location);
            }
        }

        // 2) Exit-Connection-Point-Fall: Die Referenzsuche findet die cross-file 'Instanz:<exit>'-Kanten NICHT
        //    (der über den Namen aufgelöste Origin ist der dateilokale Exit-Node). Über GetExitUsagesAsync
        //    nachziehen — genau der Kern von nav_exit_usages.
        if (symbol is IExitNodeSymbol exitNode) {
            var usages = await NavCallHierarchyService.GetExitUsagesAsync(
                exitNode.ContainingTask, exitNode.Name, workspace.Solution, cancellationToken);

            foreach (var usage in usages) {
                foreach (var site in usage.Sites) {
                    Collect(site.Location);
                }
            }
        }

        if (crossFileCount == 0) {
            return;
        }

        result.CrossFileReferenceCount = crossFileCount;
        result.CrossFileFiles          = crossFileFiles.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
        result.Warning = $"Dateilokaler Rename: {crossFileCount} Referenz(en) in {crossFileFiles.Count} weiteren " +
                         "Datei(en) werden NICHT geändert und brechen (NAV0010). Diese Dateien separat nachziehen " +
                         "(siehe nav_references/nav_exit_usages), z.B. per nav_rename je Datei.";
    }

}