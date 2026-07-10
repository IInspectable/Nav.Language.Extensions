#region Using Directives

using System;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.Formatting;
using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Nav.Language.Tests.Formatting;

/// <summary>
/// Nagelt das Vertikalmodell des <see cref="GapRenderer"/> an synthetischen Layouts fest: horizontale
/// Layouts (tight/Space) mit und ohne Inline-Kommentar, Zeilenumbrüche mit Leerzeilen-Erhalt und
/// Minimum-Auffüllung, Trailing- und Eigene-Zeile-Kommentare, Direktiven ab Spalte 0, die
/// Renderer-Schranke (nie same-line hinter einen <c>//</c>-Kommentar) sowie Skiped-Läufe (verbatim).
/// Die Lücken stammen aus echten Parses; nur das Layout wird von Hand vorgegeben.
/// </summary>
[TestFixture]
public class GapRendererTests {

    static readonly TextEditorSettings Settings = new(tabSize: 4, newLine: "\r\n");

    // ---- Horizontale Layouts ------------------------------------------------------------------

    [Test]
    public void SingleSpaceRendersExactlyOneSpace() {
        // Lücke 'task' -> 'A' (im Original ein Space).
        Assert.That(Render("task A\r\n{\r\n}\r\n", SyntaxTokenType.TaskKeyword, GapLayout.SingleSpace.Instance),
                    Is.EqualTo(" "));
    }

    [Test]
    public void SingleSpaceNormalizesWiderGaps() {
        // Lücke 'task' -> 'A' (im Original mehrere Spaces).
        Assert.That(Render("task     A\r\n{\r\n}\r\n", SyntaxTokenType.TaskKeyword, GapLayout.SingleSpace.Instance),
                    Is.EqualTo(" "));
    }

    [Test]
    public void NothingRendersTight() {
        // Lücke 'I1' -> ':' in einer Exit-Transition.
        var source = """
        task A
        {
            task B I1;
            exit E;

            I1 : Out --> E;
        }
        """;
        Assert.That(Render(source, (prev, next) => prev.ToString() == "I1" && next.Type == SyntaxTokenType.Colon,
                           GapLayout.Nothing.Instance),
                    Is.EqualTo(""));
    }

    [Test]
    public void InlineBlockCommentStaysOnLineWithSingleSpaces() {
        // Lücke 'I1' -> '-->' mit einzeiligem Block-Kommentar: Umgebungs-Whitespace -> je ein Space.
        var source = """
        task A
        {
            init I1;
            exit E;

            I1/* x */-->E;
        }
        """;
        Assert.That(Render(source, (prev, next) => prev.ToString() == "I1" && next.Type == SyntaxTokenType.GoToEdgeKeyword,
                           GapLayout.SingleSpace.Instance),
                    Is.EqualTo(" /* x */ "));
    }

    [Test]
    public void InlineBlockCommentForcesSpaceEvenWhenTight() {
        // Auch ein tight-Layout darf den Kommentar nicht an die Token kleben.
        var source = """
        task A
        {
            init I1;
            exit E;

            I1/* x */-->E;
        }
        """;
        Assert.That(Render(source, (prev, next) => prev.ToString() == "I1" && next.Type == SyntaxTokenType.GoToEdgeKeyword,
                           GapLayout.Nothing.Instance),
                    Is.EqualTo(" /* x */ "));
    }

    // ---- Vertikale Layouts --------------------------------------------------------------------

    [Test]
    public void NewLineRendersBreakAndIndent() {
        // Lücke '{' -> 'init': Umbruch auf Tiefe 1 (Tab).
        var source = """
        task A
        {
            init I1;
        }
        """;
        Assert.That(Render(source, SyntaxTokenType.OpenBrace, new GapLayout.NewLine(BlankLinesBefore: 0, IndentDepth: 1)),
                    Is.EqualTo("\r\n\t"));
    }

    [Test]
    public void NewLineRendersSpacesIndentWhenConfigured() {
        var source = """
        task A
        {
            init I1;
        }
        """;
        var options = NavFormattingOptions.Default with { IndentStyle = IndentStyle.Spaces };
        Assert.That(Render(source, SyntaxTokenType.OpenBrace, new GapLayout.NewLine(0, 1), options),
                    Is.EqualTo("\r\n    "));
    }

    [Test]
    public void NewLinePreservesAuthoredBlankLines() {
        // Zwei Autoren-Leerzeilen zwischen '{'-Zeile und 'init' bleiben erhalten (kein Kollaps).
        var source = """
        task A
        {


            init I1;
        }
        """;
        Assert.That(Render(source, SyntaxTokenType.OpenBrace, new GapLayout.NewLine(BlankLinesBefore: 2, IndentDepth: 1)),
                    Is.EqualTo("\r\n\r\n\r\n\t"));
    }

    [Test]
    public void NewLineTopsUpMissingBlankLines() {
        // Das Layout verlangt mindestens eine Leerzeile, der Autor hat keine gesetzt.
        var source = """
        task A
        {
            init I1;
        }
        """;
        Assert.That(Render(source, SyntaxTokenType.OpenBrace, new GapLayout.NewLine(BlankLinesBefore: 1, IndentDepth: 1)),
                    Is.EqualTo("\r\n\r\n\t"));
    }

    [Test]
    public void TrailingCommentStaysOnPreviousLine() {
        // Lücke ';' -> 'exit' mit Trailing-Kommentar: bleibt auf der Zeile, genau ein Space davor.
        var source = """
        task A
        {
            init I1;   // tail
            exit E;
        }
        """;
        Assert.That(Render(source, SyntaxTokenType.Semicolon, new GapLayout.NewLine(0, 1)),
                    Is.EqualTo(" // tail\r\n\t"));
    }

    [Test]
    public void OwnLineCommentIsIndentedToLinePrefix() {
        // Eigene-Zeile-Kommentar wird auf den Block-Einzug gesetzt, Text verbatim.
        var source = """
        task A
        {
            init I1;
            // Banner ----
            exit E;
        }
        """;
        Assert.That(Render(source, SyntaxTokenType.Semicolon, new GapLayout.NewLine(0, 1)),
                    Is.EqualTo("\r\n\t// Banner ----\r\n\t"));
    }

    [Test]
    public void InlineCommentBeforeNextTokenKeepsSingleSpaceAfter() {
        // Kommentar auf der Zeile von Next (vor dem Token): Präfix, Kommentar, ein Space.
        var source = """
        task A
        {
            init I1;
            /* x */ exit E;
        }
        """;
        Assert.That(Render(source, SyntaxTokenType.Semicolon, new GapLayout.NewLine(0, 1)),
                    Is.EqualTo("\r\n\t/* x */ "));
    }

    [Test]
    public void DirectiveStaysOnOwnLineAtColumnZero() {
        // Eine (auch eingerückte) Direktive zwischen Membern bleibt auf eigener Zeile ab Spalte 0, verbatim.
        var source = """
        task A
        {
        }
            #pragma version 1
        task B
        {
        }
        """;
        Assert.That(Render(source, (prev, next) => prev.Type == SyntaxTokenType.CloseBrace && next.Type == SyntaxTokenType.TaskKeyword,
                           new GapLayout.NewLine(0, 0)),
                    Is.EqualTo("\r\n#pragma version 1\r\n"));
    }

    // ---- Renderer-Schranke & Verbatim ---------------------------------------------------------

    [Test]
    public void RendererBarrierNeverMergesLineAfterSingleLineComment() {
        // Ein horizontales Layout über eine '//'-Lücke degradiert zum Umbruch auf Block-Einzug —
        // sonst verschluckte der Kommentar das folgende Token.
        var source = """
        task A
        {
            init I1; // tail
            exit E;
        }
        """;
        Assert.That(Render(source, SyntaxTokenType.Semicolon, GapLayout.SingleSpace.Instance),
                    Is.EqualTo(" // tail\r\n\t"));
    }

    [Test]
    public void VerbatimReturnsOriginalGapText() {
        Assert.That(Render("task     A\r\n{\r\n}\r\n", SyntaxTokenType.TaskKeyword, GapLayout.Verbatim.Instance),
                    Is.EqualTo("     "));
    }

    [Test]
    public void SkippedTokensGapStaysVerbatimRegardlessOfLayout() {
        // Recovery-Lauf in der Lücke: byte-genau erhalten, egal welches Layout die Regel verlangt hätte.
        var source = """
        task A
        {
            init I1;
            @@@
            exit E;
        }
        """;
        var gap = FindGap(source, SyntaxTokenType.Semicolon);

        Assert.That(gap.Ctx.Trivia.HasSkippedTokens, Is.True, "Testaufbau: die Lücke muss den Skiped-Lauf enthalten.");
        Assert.That(RenderGap(gap, GapLayout.SingleSpace.Instance, NavFormattingOptions.Default),
                    Is.EqualTo(gap.OriginalText));
    }

    // ---- Helpers ------------------------------------------------------------------------------

    sealed record TestGap(SyntaxTree Tree, GapContext Ctx, string OriginalText);

    static string Render(string source, SyntaxTokenType prevType, GapLayout layout) {
        return Render(source, prevType, layout, NavFormattingOptions.Default);
    }

    static string Render(string source, SyntaxTokenType prevType, GapLayout layout, NavFormattingOptions options) {
        return Render(source, (prev, next) => prev.Type == prevType, layout, options);
    }

    static string Render(string source, Func<SyntaxToken, SyntaxToken, bool> gapSelector, GapLayout layout) {
        return Render(source, gapSelector, layout, NavFormattingOptions.Default);
    }

    static string Render(string source, Func<SyntaxToken, SyntaxToken, bool> gapSelector, GapLayout layout, NavFormattingOptions options) {
        return RenderGap(FindGap(source, gapSelector), layout, options);
    }

    static string RenderGap(TestGap gap, GapLayout layout, NavFormattingOptions options) {
        return new GapRenderer(gap.Tree.SourceText, Settings, options).Render(gap.Ctx, layout);
    }

    static TestGap FindGap(string source, SyntaxTokenType prevType) {
        return FindGap(source, (prev, next) => prev.Type == prevType);
    }

    /// <summary>
    /// Die erste Lücke, deren <c>(Prev, Next)</c>-Paar der Auswahl entspricht — bewusst über den flachen
    /// Token-Strom statt <see cref="SyntaxToken.NextToken()"/> (der ist parent-lokal und liefert an
    /// Knotengrenzen <see cref="SyntaxToken.Missing"/>). IndentDepth 1 innerhalb eines Task-Bodys, sonst 0.
    /// </summary>
    static TestGap FindGap(string source, Func<SyntaxToken, SyntaxToken, bool> gapSelector) {

        var tree   = SyntaxTree.ParseText(source);
        var tokens = tree.Tokens;

        for (var i = 0; i < tokens.Count - 1; i++) {

            var prev = tokens[i];
            var next = tokens[i + 1];

            if (!gapSelector(prev, next)) {
                continue;
            }

            var indentDepth = IsInTaskBody(next) ? 1 : 0;
            var ctx         = new GapContext(prev, next, indentDepth, GapTrivia.Create(prev, next),
                                             isSuppressed: false, alignment: AlignmentMap.Empty);

            return new TestGap(tree, ctx, tree.SourceText.Substring(ctx.Extent));
        }

        throw new InvalidOperationException("Testaufbau: kein Token entspricht der Auswahl.");
    }

    static bool IsInTaskBody(SyntaxToken token) {

        foreach (var node in token.Parent?.AncestorsAndSelf() ?? Array.Empty<SyntaxNode>()) {
            if (node is TaskDefinitionSyntax task && !task.OpenBrace.IsMissing &&
                token.Start >= task.OpenBrace.End &&
                (task.CloseBrace.IsMissing || token.Start < task.CloseBrace.Start)) {
                return true;
            }
        }

        return false;
    }

}
