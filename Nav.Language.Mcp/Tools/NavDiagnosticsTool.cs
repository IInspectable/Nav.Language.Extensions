#region Using Directives

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ModelContextProtocol.Server;

using Pharmatechnik.Nav.Utilities.IO;

#endregion

namespace Pharmatechnik.Nav.Language.Mcp.Tools;

/// <summary>
/// MCP-Tool <c>nav_diagnostics</c>: das workspace-weite Gegenstück zu <c>nav_validate</c>. Validiert alle
/// (bzw. per <c>filter</c> eingegrenzten) <c>.nav</c>-Dateien der Solution und aggregiert die Diagnostics.
/// Keine eigene Validierungslogik — fan-out über dieselbe Engine-Schiene wie <c>nav_validate</c>
/// (<see cref="NavMcpWorkspace.GetFileDiagnostics"/> → <c>DiagnosticsComputer.FromUnit</c>), fresh pro Datei.
/// </summary>
[McpServerToolType]
public static class NavDiagnosticsTool {

    /// <summary>Voreinstellung für die Seitengröße, falls der Aufrufer keine angibt.</summary>
    const int DefaultLimit = 100;

    /// <summary>
    /// Obergrenze für die Seitengröße — so gewählt, dass selbst eine voll gefüllte Seite sicher unter dem
    /// Tool-Result-Token-Limit (~25k Tokens) bleibt (analog zu <see cref="NavWorkspaceTool"/>).
    /// </summary>
    const int MaxLimit = 200;

    [McpServerTool(Name = "nav_diagnostics")]
    [Description("Validates all .nav files in the workspace (or a filtered subset) and returns aggregated " +
                 "diagnostics — the workspace-wide counterpart to nav_validate. Use it to answer 'are there " +
                 "any warnings/errors in the workspace (or in module X)'. 'summary' gives per-severity totals " +
                 "and 'count' the total number of diagnostics (both BEFORE paging). Large result sets are paged: " +
                 "at most 'limit' diagnostics are returned (default 100, max 200); 'truncated' = true means there " +
                 "are more — narrow via 'filter'/'severity' or page with 'offset'. Without 'filter' this scans the " +
                 "whole workspace and is deliberately expensive; prefer narrowing to a module. Line/column are 1-based.")]
    public static async Task<NavDiagnosticsResult> Diagnostics(
        NavMcpWorkspace workspace,
        [Description("Optional case-insensitive substring matched against each file's relative path; only matching " +
                     "files are validated. Use it to scope to a subfolder/module and avoid a full workspace sweep.")]
        string? filter = null,
        [Description("Optional severity filter: 'error', 'warning' or 'suggestion' (case-insensitive). Null = all.")]
        string? severity = null,
        [Description("Max number of diagnostics (not files) to return (default 100, capped at 200). Page with 'offset'.")]
        int limit = DefaultLimit,
        [Description("Number of (filtered) diagnostics to skip before returning — for paging.")]
        int offset = 0,
        CancellationToken cancellationToken = default) {

        await workspace.EnsureSolutionLoadedAsync(cancellationToken);

        var root = workspace.SolutionDirectory?.FullName;

        // 1. Dateimenge nach filter bestimmen (Muster aus NavWorkspaceTool: relativer Pfad + Substring).
        var files = workspace.Solution.SolutionFiles
                             .Select(file => new {
                                  File = file,
                                  Rel  = root != null ? PathHelper.GetRelativePath(root, file.FullName) : file.FullName
                              })
                             .Where(entry => string.IsNullOrEmpty(filter) ||
                                             entry.Rel.Contains(filter, StringComparison.OrdinalIgnoreCase))
                             .OrderBy(entry => entry.Rel, StringComparer.OrdinalIgnoreCase)
                             .ToList();

        // Severity-Filter auf den kanonischen Engine-Namen normalisieren (null = alle).
        var severityFilter = NormalizeSeverity(severity);

        // 2. Pro Datei fresh validieren (wie nav_validate) und aggregieren.
        var all                  = new List<NavWorkspaceDiagnosticDto>();
        var filesScanned         = 0;
        var filesWithDiagnostics = 0;
        var summary              = new DiagnosticsSummary();

        foreach (var entry in files) {

            cancellationToken.ThrowIfCancellationRequested();

            var diagnostics = workspace.GetFileDiagnostics(entry.File.FullName);
            if (diagnostics == null) {
                // Nicht gefunden/nicht parsebar — überspringen (zählt nicht als gescannt).
                continue;
            }

            filesScanned++;

            var matched = severityFilter == null
                ? diagnostics
                : diagnostics.Where(diagnostic => string.Equals(diagnostic.Severity, severityFilter, StringComparison.OrdinalIgnoreCase)).ToList();

            if (matched.Count == 0) {
                continue;
            }

            filesWithDiagnostics++;

            foreach (var diagnostic in matched) {
                all.Add(NavWorkspaceDiagnosticDto.From(diagnostic, entry.File.FullName, entry.Rel));
                CountInto(summary, diagnostic.Severity);
            }
        }

        // 3. Paging über die aggregierten Diagnostics.
        var safeOffset = Math.Max(0, offset);
        var safeLimit  = limit <= 0 ? DefaultLimit : Math.Min(limit, MaxLimit);
        var page       = all.Skip(safeOffset).Take(safeLimit).ToList();

        return new NavDiagnosticsResult {
            Root                 = root,
            FilesScanned         = filesScanned,
            FilesWithDiagnostics = filesWithDiagnostics,
            Summary              = summary,
            Count                = all.Count,
            Returned             = page.Count,
            Offset               = safeOffset,
            Limit                = safeLimit,
            Truncated            = safeOffset + page.Count < all.Count,
            Diagnostics          = page
        };
    }

    /// <summary>Normalisiert die Severity-Eingabe auf den kanonischen Engine-Namen; <c>null</c> = alle.</summary>
    static string? NormalizeSeverity(string? severity) {

        if (string.IsNullOrWhiteSpace(severity)) {
            return null;
        }

        foreach (var name in new[] {
                     nameof(DiagnosticSeverity.Error),
                     nameof(DiagnosticSeverity.Warning),
                     nameof(DiagnosticSeverity.Suggestion)
                 }) {

            if (string.Equals(severity, name, StringComparison.OrdinalIgnoreCase)) {
                return name;
            }
        }

        // Unbekannte Eingabe -> kein Filter (kein falsches "nichts gefunden").
        return null;
    }

    static void CountInto(DiagnosticsSummary summary, string severity) {

        if (string.Equals(severity, nameof(DiagnosticSeverity.Error), StringComparison.OrdinalIgnoreCase)) {
            summary.Error++;
        } else if (string.Equals(severity, nameof(DiagnosticSeverity.Warning), StringComparison.OrdinalIgnoreCase)) {
            summary.Warning++;
        } else if (string.Equals(severity, nameof(DiagnosticSeverity.Suggestion), StringComparison.OrdinalIgnoreCase)) {
            summary.Suggestion++;
        }
    }
}
