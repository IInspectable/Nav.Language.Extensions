#region Using Directives

using System.Linq;
using System.Text;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;

#endregion

namespace Nav.Language.Tests;

/// <summary>
/// Prüft die additive Trivia-Sicht am Baum (<see cref="SyntaxTree.DescendantTrivia"/>,
/// <see cref="SyntaxTree.Comments"/>, <see cref="SyntaxTree.FindTrivia"/>,
/// <see cref="SyntaxTree.IsPositionInComment"/>) — die Brücke weg vom flachen Trivia-Strom hin zum
/// angehängten Roslyn-Modell. Anders als <see cref="TokenTriviaTests"/> (Zuordnungsregel je Token)
/// zielt diese Suite auf die datei-weite Aggregat-Sicht.
/// </summary>
[TestFixture]
public class TriviaViewTests {

    // DescendantTrivia liefert jede Trivia genau einmal, aufsteigend und überlappungsfrei; zusammen mit
    // den signifikanten Token deckt sie den Quelltext lückenlos ab.
    [Test]
    public void DescendantTriviaReconstructsAllTriviaInSourceOrder() {

        var source = "// header\r\ntask A /* x */\r\n{\r\n    init I1;  // c\r\n\r\n    exit e1;\r\n    I1 --> e1;\r\n}\r\n";
        var tree   = SyntaxTree.ParseText(source);

        var trivia = tree.DescendantTrivia().ToList();

        for (var i = 1; i < trivia.Count; i++) {
            Assert.That(trivia[i].Start, Is.GreaterThanOrEqualTo(trivia[i - 1].End),
                        "Trivia müssen aufsteigend und überlappungsfrei sein.");
        }

        // Signifikante Token (ohne Trivia-Token, ohne EndOfFile) plus alle Trivia, nach Position sortiert,
        // ergeben den Quelltext wieder Zeichen für Zeichen.
        var pieces = tree.Tokens
                         .Where(t => !IsTriviaType(t.Type) && t.Type != SyntaxTokenType.EndOfFile)
                         .Select(t => (t.Start, Text: t.ToString()))
                         .Concat(trivia.Select(t => (t.Start, Text: t.ToString(tree.SourceText))))
                         .OrderBy(p => p.Start);

        var sb = new StringBuilder();
        foreach (var piece in pieces) {
            sb.Append(piece.Text);
        }

        Assert.That(sb.ToString(), Is.EqualTo(source));
    }

    // Comments() filtert auf die Kommentar-Trivia. Bei CRLF bleibt — wie vom Lexer dokumentiert — das '\r'
    // Teil des SingleLineComment-Tokens; nur das '\n' ist die separate NewLine-Trivia.
    [Test]
    public void CommentsYieldsOnlyComments() {

        var source = "// a\r\ntask A /* b */\r\n{\r\n}\r\n";
        var tree   = SyntaxTree.ParseText(source);

        var comments = tree.Comments().ToList();

        Assert.That(comments.All(c => c.IsComment));
        Assert.That(comments.Select(c => c.ToString(tree.SourceText)),
                    Is.EqualTo(new[] { "// a\r", "/* b */" }));
    }

    // FindTrivia/IsPositionInComment treffen die Trivia am Halbintervall [Start, End) — wie FindToken bei
    // den Token.
    [Test]
    public void FindTriviaHitsCommentAndRespectsHalfOpenInterval() {

        var source = "task A /* block */\r\n{\r\n}\r\n";
        var tree   = SyntaxTree.ParseText(source);

        var commentStart = source.IndexOf("/* block */", System.StringComparison.Ordinal);
        var commentEnd   = commentStart + "/* block */".Length;

        // Mitten im Kommentar.
        Assert.That(tree.IsPositionInComment(commentStart + 3), Is.True);
        Assert.That(tree.FindTrivia(commentStart + 3).Type, Is.EqualTo(SyntaxTokenType.MultiLineComment));

        // Auf dem ersten Zeichen des Kommentars (inklusiv).
        Assert.That(tree.IsPositionInComment(commentStart), Is.True);

        // Direkt hinter dem Kommentar (End exklusiv) ist es bereits die folgende Trivia, kein Kommentar mehr.
        Assert.That(tree.IsPositionInComment(commentEnd), Is.False);
    }

    // Auf einem signifikanten Token bzw. in reinem Whitespace ist die Position kein Kommentar.
    [Test]
    public void IsPositionInCommentIsFalseOutsideComments() {

        var source = "task A // tail\r\n{\r\n}\r\n";
        var tree   = SyntaxTree.ParseText(source);

        Assert.That(tree.IsPositionInComment(source.IndexOf('A')), Is.False);             // auf dem Bezeichner
        Assert.That(tree.IsPositionInComment(source.IndexOf("task", System.StringComparison.Ordinal) + 4), Is.False); // im Whitespace nach 'task'
        Assert.That(tree.IsPositionInComment(-1), Is.False);                              // ungültige Position
        Assert.That(tree.FindTrivia(-1), Is.EqualTo(default(SyntaxTrivia)));
    }

    static bool IsTriviaType(SyntaxTokenType type) {
        return type == SyntaxTokenType.Whitespace        ||
               type == SyntaxTokenType.NewLine           ||
               type == SyntaxTokenType.SingleLineComment ||
               type == SyntaxTokenType.MultiLineComment;
    }
}
