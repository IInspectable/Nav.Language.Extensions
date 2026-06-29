#region Using Directives

using System.Collections.Generic;
using System.Linq;

using Pharmatechnik.Nav.Language.Text;

using Protocol = Microsoft.VisualStudio.LanguageServer.Protocol;

#endregion

namespace Pharmatechnik.Nav.Language.Lsp;

/// <summary>
/// Baut die zusammenklappbaren Bereiche (<c>textDocument/foldingRange</c>) rein syntaktisch aus dem
/// <see cref="SyntaxTree"/> — analog zum VS-<c>OutliningTagger</c>, aber zeilenbasiert (LSP faltet
/// von <c>startLine</c> bis <c>endLine</c>). Gefaltet werden mehrzeilige using-Blöcke, taskref-Include-
/// Blöcke, Task-Definitionen/-Deklarationen, Node- und Transition-Blöcke sowie mehrzeilige Kommentare.
/// </summary>
static class FoldingRangeBuilder {

    public static Protocol.FoldingRange[] Build(SyntaxTree syntaxTree) {

        var source = syntaxTree.SourceText;
        var root   = syntaxTree.Root;

        // Dedup über (startLine,endLine,kind): ein Task-Body, der nur aus einem Block besteht, würde
        // sonst denselben Bereich doppelt liefern.
        var seen   = new HashSet<(int, int, Protocol.FoldingRangeKind?)>();
        var result = new List<Protocol.FoldingRange>();

        void Add(int startOffset, int endOffset, Protocol.FoldingRangeKind? kind) {

            if (endOffset <= startOffset) {
                return;
            }

            var location  = source.GetLocation(TextExtent.FromBounds(start: startOffset, end: endOffset));
            var startLine = location.StartLine;
            var endLine   = location.EndLine;

            if (endLine <= startLine) {
                return;
            }

            if (!seen.Add((startLine, endLine, kind))) {
                return;
            }

            result.Add(new Protocol.FoldingRange {
                StartLine = startLine,
                EndLine   = endLine,
                Kind      = kind
            });
        }

        void AddNode(SyntaxNode node, Protocol.FoldingRangeKind? kind) {
            if (node != null && !node.Extent.IsEmptyOrMissing) {
                Add(node.Extent.Start, node.Extent.End, kind);
            }
        }

        AddUsingBlock(root, Add);
        AddIncludeBlocks(root, Add);

        foreach (var task in root.DescendantNodes<TaskDefinitionSyntax>()) {
            AddNode(task, Protocol.FoldingRangeKind.Region);
        }

        foreach (var taskRef in root.DescendantNodes<TaskDeclarationSyntax>()) {
            AddNode(taskRef, Protocol.FoldingRangeKind.Region);
        }

        foreach (var block in root.DescendantNodes<NodeDeclarationBlockSyntax>()) {
            AddNode(block, Protocol.FoldingRangeKind.Region);
        }

        foreach (var block in root.DescendantNodes<TransitionDefinitionBlockSyntax>()) {
            AddNode(block, Protocol.FoldingRangeKind.Region);
        }

        // Mehrzeilige Kommentare aus der angehängten Trivia (Roslyn-Modell), nicht mehr aus dem flachen Strom.
        foreach (var comment in syntaxTree.DescendantTrivia().Where(t => t.Type == SyntaxTokenType.MultiLineComment)) {
            if (!comment.Extent.IsEmptyOrMissing) {
                Add(comment.Extent.Start, comment.Extent.End, Protocol.FoldingRangeKind.Comment);
            }
        }

        return result.ToArray();
    }

    /// <summary>Faltet einen zusammenhängenden Block von ≥2 using-Direktiven (Import-Region).</summary>
    static void AddUsingBlock(SyntaxNode root, System.Action<int, int, Protocol.FoldingRangeKind?> add) {

        var usings = root.DescendantNodes<CodeUsingDeclarationSyntax>().ToList();
        if (usings.Count < 2) {
            return;
        }

        var first = usings[0];
        var last  = usings[usings.Count - 1];

        add(first.Extent.Start, last.Extent.End, Protocol.FoldingRangeKind.Imports);
    }

    /// <summary>
    /// Faltet zusammenhängende Läufe von <c>taskref "datei"</c>-Includes (≥2) zu je einer Import-Region —
    /// ein Lauf endet, sobald ein anderer Top-Level-Knoten dazwischenliegt.
    /// </summary>
    static void AddIncludeBlocks(SyntaxNode root, System.Action<int, int, Protocol.FoldingRangeKind?> add) {

        var relevant = root.DescendantNodes<TaskDefinitionSyntax>().Cast<SyntaxNode>()
                           .Concat(root.DescendantNodes<TaskDeclarationSyntax>())
                           .Concat(root.DescendantNodes<IncludeDirectiveSyntax>())
                           .OrderBy(n => n.Extent.Start);

        IncludeDirectiveSyntax? first = null;
        IncludeDirectiveSyntax? last  = null;

        void Flush() {
            if (first != null && first != last) {
                add(first.Extent.Start, last!.Extent.End, Protocol.FoldingRangeKind.Imports);
            }

            first = last = null;
        }

        foreach (var node in relevant) {
            if (node is IncludeDirectiveSyntax include) {
                first ??= include;
                last  =   include;
            } else {
                Flush();
            }
        }

        Flush();
    }
}
