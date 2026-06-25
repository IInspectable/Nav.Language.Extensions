#region Using Directives

using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ModelContextProtocol.Server;

using Pharmatechnik.Nav.Language.FindReferences;
using Pharmatechnik.Nav.Language.References;

#endregion

namespace Pharmatechnik.Nav.Language.Mcp.Tools;

/// <summary>
/// MCP-Tool <c>nav_references</c>: findet alle solution-weiten Vorkommen eines Task- oder Knotennamens.
/// </summary>
[McpServerToolType]
public static class NavReferencesTool {

    [McpServerTool(Name = "nav_references")]
    [Description("Finds all references to a task or node across the whole workspace (solution-wide), including " +
                 "the declaration itself (marked isDeclaration). Returns 1-based file/line/column per occurrence. " +
                 "Use this before renaming or removing a symbol to see where it is used. If the name is ambiguous, " +
                 "returns candidates — pass 'task' to disambiguate.")]
    public static async Task<NavReferencesResult> References(
        NavMcpWorkspace workspace,
        [Description("Absolute path to the .nav file the name lives in.")]
        string path,
        [Description("Name of the task or node whose references to find.")]
        string name,
        [Description("Optional task name to scope a node lookup (disambiguation).")]
        string? task = null,
        CancellationToken cancellationToken = default) {

        var result = new NavReferencesResult { Path = path, Name = name };

        // Solution-weite Suche braucht die geladene Solution.
        await workspace.EnsureSolutionLoadedAsync(cancellationToken);

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

        // Exakt der LSP-Pfad: das Symbol an der Position als Ursprung, dann die Engine-Referenzsuche.
        var origin = NavReferenceService.FindSymbol(unit, symbol!.Location.Start);
        if (origin == null) {
            result.Error = NavNameResolution.NotFoundMessage(name, path);
            return result;
        }

        var collector = new McpReferenceCollector(includeDeclaration: true, cancellationToken);
        var args      = new FindReferencesArgs(origin, unit, workspace.Solution, collector);

        await ReferenceFinder.FindReferencesAsync(args);

        result.Locations = collector.Results
                                    .Select(item => NavLocationDto.From(item.Location, item.IsDeclaration))
                                    .ToList();
        result.Count = result.Locations.Count;
        return result;
    }
}
