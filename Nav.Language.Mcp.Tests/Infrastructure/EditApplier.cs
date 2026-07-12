#region Using Directives

using System.Collections.Generic;
using System.Linq;

using Pharmatechnik.Nav.Language.Mcp.Tools;
using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Nav.Language.Mcp.Tests.Infrastructure;

/// <summary>
/// Wendet ein von den mutierenden Tools (<c>nav_rename</c>, <c>nav_code_actions</c>) geliefertes Edit-Set auf
/// einen Text an — die Tools schreiben ja nichts selbst, sondern liefern nur die Edits. Bildet die 1-basierten
/// <see cref="NavEditDto"/>-Bereiche über die Zeilenliste des <see cref="SourceText"/> auf Offsets ab (identisch
/// zur Engine-Zählung) und spleißt die Ersetzungen absteigend ein, damit frühere Edits die Offsets späterer
/// nicht verschieben.
/// </summary>
static class EditApplier {

    public static string Apply(string text, IReadOnlyList<NavEditDto> edits) {

        var sourceText = SourceText.From(text);

        var resolved = edits
                      .Select(edit => new {
                           Start = ToOffset(sourceText, edit.Line,    edit.Column),
                           End   = ToOffset(sourceText, edit.EndLine, edit.EndColumn),
                           edit.NewText
                       })
                      .OrderByDescending(edit => edit.Start)
                      .ToList();

        foreach (var edit in resolved) {
            text = text.Substring(0, edit.Start) + edit.NewText + text.Substring(edit.End);
        }

        return text;
    }

    // 1-basierte Zeile/Spalte → absoluter Offset (Zeilenstart aus der SourceText-Zeilenliste + 0-basierte Spalte).
    static int ToOffset(SourceText sourceText, int line, int column) =>
        sourceText.TextLines[line - 1].Start + (column - 1);

}
