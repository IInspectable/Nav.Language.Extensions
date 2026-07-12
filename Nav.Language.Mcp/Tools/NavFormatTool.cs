#region Using Directives

using System;
using System.Collections.Generic;
using System.ComponentModel;

using ModelContextProtocol.Server;

using Pharmatechnik.Nav.Language.Formatting;
using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.Mcp.Tools;

/// <summary>
/// MCP-Tool <c>nav_format</c>: berechnet die Formatierungs-Edits für ein ganzes Dokument oder einen
/// Zeilenbereich (read-only). Engine-Kern ist <see cref="NavFormattingService"/> — derselbe Gap-Rewriter,
/// den der LSP-Server (<c>textDocument/formatting</c>/<c>…/rangeFormatting</c>) und die VS-Extension
/// nutzen. Der Formatter ist rein syntaktisch → der <see cref="SyntaxTree"/> genügt (kein Semantik-Build
/// wie bei Rename).
/// </summary>
[McpServerToolType]
public static class NavFormatTool {

    [McpServerTool(Name = "nav_format")]
    [Description("Formats a Nav (.nav) file — the whole document, or only the lines startLine..endLine — and " +
                 "returns the edits (1-based ranges + new text) plus the complete formatted document text. "   +
                 "Does NOT modify any file — apply the result yourself (easiest: overwrite the file with "     +
                 "'formattedText'). The formatter only rewrites the whitespace between tokens (indentation, "  +
                 "column alignment, blank-line caps, final newline); it never changes token text. Empty "      +
                 "'edits' means the document (or range) is already formatted.")]
    public static NavFormatResult Format(
        NavMcpWorkspace workspace,
        [Description("Absolute path to the .nav file.")]
        string path,
        [Description("Optional 1-based first line of the range to format. Omit startLine and endLine to " +
                     "format the whole document.")]
        int? startLine = null,
        [Description("Optional 1-based last line of the range to format (inclusive). Defaults to the last " +
                     "line of the file if only startLine is given.")]
        int? endLine = null,
        [Description("Indent using spaces instead of tabs. Default: tabs (the canonical Nav formatter default).")]
        bool? insertSpaces = null,
        [Description("Indent width in characters (spaces per level / assumed tab width). Default: 4.")]
        int? tabSize = null,
        [Description("Whether to include the complete formatted document text as 'formattedText'. Set to " +
                     "false to keep the result small for large files (edits only).")]
        bool includeFormattedText = true) {

        var result = new NavFormatResult { Path = path };

        if (tabSize is < 1) {
            result.Error = "'tabSize' must be at least 1.";
            return result;
        }

        var syntaxTree = workspace.GetFreshSyntaxTree(path, out _);
        if (syntaxTree == null) {
            result.Error = NavOutlineResult.NotFound(path).Error;
            return result;
        }

        var sourceText = syntaxTree.SourceText;
        var settings   = NavEditorSettings.For(sourceText, tabSize ?? 4);
        var options    = FormattingOptionsFor(insertSpaces, tabSize);

        IReadOnlyList<TextChange> changes;

        if (startLine == null && endLine == null) {
            changes = NavFormattingService.FormatDocument(syntaxTree, settings, options);
        } else {

            var lines = sourceText.TextLines;
            var first = startLine ?? 1;
            var last  = endLine   ?? lines.Count;

            if (first < 1 || last < first || first > lines.Count) {
                result.Error = $"Invalid line range {first}..{last} — the file has {lines.Count} lines.";
                return result;
            }

            // Ein über das Dateiende hinausragendes endLine wird geklemmt (agentenfreundlich) — nur ein
            // komplett außerhalb liegender Start ist ein Fehler.
            last = Math.Min(last, lines.Count);

            var range = TextExtent.FromBounds(lines[first - 1].Start, lines[last - 1].End);
            changes = NavFormattingService.FormatRange(syntaxTree, range, settings, options);
        }

        result.Edits = NavEditDto.FromChanges(sourceText, changes);

        if (includeFormattedText && changes.Count > 0) {
            result.FormattedText = new TextChangeWriter().ApplyTextChanges(sourceText.Text, changes);
        }

        return result;
    }

    /// <summary>
    /// Bildet die optionalen Tool-Parameter auf die <see cref="NavFormattingOptions"/> ab: nur explizit
    /// übergebene Werte überschreiben die kanonischen Vorgaben von <see cref="NavFormattingOptions.Default"/>
    /// (Tabs, Breite 4 — Korpus-Mehrheit). Anders als beim LSP-Server gibt es keinen Editor-Konfig-Kanal,
    /// der immer Werte liefert — der Default ist hier die kanonische Formatter-Vorgabe, nicht der Editor.
    /// </summary>
    static NavFormattingOptions FormattingOptionsFor(bool? insertSpaces, int? tabSize) {

        var options = NavFormattingOptions.Default;

        if (insertSpaces != null) {
            options = options with { IndentStyle = insertSpaces.Value ? IndentStyle.Spaces : IndentStyle.Tabs };
        }

        if (tabSize != null) {
            options = options with { IndentSize = tabSize.Value };
        }

        return options;
    }

}
