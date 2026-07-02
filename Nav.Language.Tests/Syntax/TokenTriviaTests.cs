#region Using Directives

using System.Collections.Generic;
using System.Linq;
using System.Text;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Nav.Language.Tests;

/// <summary>
/// Nagelt die Roslyn-Zuordnungsregel der an die <see cref="SyntaxToken"/> angehängten Leading-/
/// Trailing-Trivia gezielt an ihren Kanten fest — das Gegenstück zum korpusweiten <c>.trivia</c>-Golden
/// in <see cref="SyntaxGoldenTests"/>. Anders als <see cref="SyntaxNodeTriviaTests"/> (das die
/// <c>Get*TriviaExtent</c>-Heuristik über den flachen Token-Strom prüft) zielt diese Suite auf das
/// echte angehängte Modell: <see cref="SyntaxToken.LeadingTrivia"/> / <see cref="SyntaxToken.TrailingTrivia"/>.
/// </summary>
[TestFixture]
public class TokenTriviaTests {

    // Leading des ersten signifikanten Tokens reicht bis zum Dateianfang und nimmt den Kopf-Kommentar mit.
    [Test]
    public void LeadingTriviaOfFirstTokenReachesFileStart() {

        var source = "// header\r\ntask A\r\n{\r\n}\r\n";
        var tree   = SyntaxTree.ParseText(source);

        var taskKeyword = First(tree, SyntaxTokenType.TaskKeyword);

        Assert.That(taskKeyword.LeadingTrivia.Select(t => t.Type),
                    Is.EqualTo(new[] { SyntaxTokenType.SingleLineComment, SyntaxTokenType.NewLine }));
        Assert.That(taskKeyword.LeadingTrivia.First().Start, Is.EqualTo(0));
        Assert.That(TriviaText(taskKeyword.LeadingTrivia, tree), Is.EqualTo("// header\r\n"));
        // 'task' und 'A' stehen in derselben Zeile: das Leerzeichen dazwischen ist Trailing von 'task'.
        Assert.That(TriviaText(taskKeyword.TrailingTrivia, tree), Is.EqualTo(" "));
    }

    // Trailing eines Tokens endet einschließlich des ersten Zeilenendes; was danach kommt (Einrückung der
    // Folgezeile) ist bereits Leading des nächsten Tokens.
    [Test]
    public void TrailingTriviaStopsAtFirstNewLineInclusive() {

        var source = "task A\r\n{\r\n    init I1;  // tail\r\n    exit e1;\r\n}\r\n";
        var tree   = SyntaxTree.ParseText(source);

        var semicolon = First(tree, SyntaxTokenType.Semicolon); // das ';' hinter 'init I1'

        Assert.That(semicolon.TrailingTrivia.Select(t => t.Type),
                    Is.EqualTo(new[] { SyntaxTokenType.Whitespace, SyntaxTokenType.SingleLineComment, SyntaxTokenType.NewLine }));
        Assert.That(TriviaText(semicolon.TrailingTrivia, tree), Is.EqualTo("  // tail\r\n"));

        var exitKeyword = First(tree, SyntaxTokenType.ExitKeyword);
        Assert.That(exitKeyword.LeadingTrivia.Select(t => t.Type),
                    Is.EqualTo(new[] { SyntaxTokenType.Whitespace }));
        Assert.That(TriviaText(exitKeyword.LeadingTrivia, tree), Is.EqualTo("    "));
    }

    // Steht das nächste Token in derselben Zeile (kein Zeilenende dazwischen), wird die gesamte Trivia zur
    // Trailing-Trivia des vorigen Tokens — das nächste Token bekommt keine Leading-Trivia.
    [Test]
    public void TrailingTriviaTakesAllWhenNoNewLineBeforeNextToken() {

        var source = "task A\r\n{\r\n    init I1; /* mid */ exit e1;\r\n}\r\n";
        var tree   = SyntaxTree.ParseText(source);

        var semicolon = First(tree, SyntaxTokenType.Semicolon); // ';' hinter 'init I1'

        Assert.That(semicolon.TrailingTrivia.Select(t => t.Type),
                    Is.EqualTo(new[] { SyntaxTokenType.Whitespace, SyntaxTokenType.MultiLineComment, SyntaxTokenType.Whitespace }));
        Assert.That(TriviaText(semicolon.TrailingTrivia, tree), Is.EqualTo(" /* mid */ "));

        var exitKeyword = First(tree, SyntaxTokenType.ExitKeyword);
        Assert.That(exitKeyword.LeadingTrivia, Is.Empty);
    }

    // Ein mehrzeiliger Block-Kommentar ist genau eine Trivia (kein Split an den eingebetteten Zeilenumbrüchen).
    [Test]
    public void MultiLineCommentAttachesAsSingleLeadingTrivia() {

        var source = "/* a\r\n b\r\n c */task A\r\n{\r\n}\r\n";
        var tree   = SyntaxTree.ParseText(source);

        var taskKeyword = First(tree, SyntaxTokenType.TaskKeyword);

        Assert.That(taskKeyword.LeadingTrivia.Select(t => t.Type),
                    Is.EqualTo(new[] { SyntaxTokenType.MultiLineComment }));
        Assert.That(TriviaText(taskKeyword.LeadingTrivia, tree), Is.EqualTo("/* a\r\n b\r\n c */"));
    }

    // Die finale Datei-Trivia (Leerzeilen, Abschluss-Kommentar) hängt als Leading-Trivia am EndOfFile.
    [Test]
    public void EndOfFileCarriesTrailingFileTrivia() {

        var source = "task A\r\n{\r\n}\r\n\r\n// trailing\r\n";
        var tree   = SyntaxTree.ParseText(source);

        var closeBrace = First(tree, SyntaxTokenType.CloseBrace);
        Assert.That(TriviaText(closeBrace.TrailingTrivia, tree), Is.EqualTo("\r\n")); // nur bis zum ersten Zeilenende

        var eof = First(tree, SyntaxTokenType.EndOfFile);
        Assert.That(eof.LeadingTrivia.Select(t => t.Type),
                    Is.EqualTo(new[] { SyntaxTokenType.NewLine, SyntaxTokenType.SingleLineComment, SyntaxTokenType.NewLine }));
        Assert.That(TriviaText(eof.LeadingTrivia, tree), Is.EqualTo("\r\n// trailing\r\n"));
    }

    // Eine Präprozessor-Direktive steht nicht mehr als Token im flachen Strom, sondern ist ein einziges
    // strukturiertes DirectiveTrivia-Stück (mit Verweis auf ihren Knoten) in der Leading-Trivia des ersten
    // echten Tokens danach; ihr Zeilenende folgt als eigenes NewLine.
    [Test]
    public void PreprocessorDirectiveIsStructuredTriviaNotInTokenStream() {

        var source = "#if DEBUG\r\ntask A\r\n{\r\n}\r\n";
        var tree   = SyntaxTree.ParseText(source);

        // Kein Präprozessor-Token verbleibt im flachen Strom.
        Assert.That(tree.Tokens.Any(t => t.Type == SyntaxTokenType.HashToken            ||
                                         t.Type == SyntaxTokenType.PreprocessorKeyword ||
                                         t.Type == SyntaxTokenType.PreprocessorText    ||
                                         t.Type == SyntaxTokenType.PreprocessorNewLine ||
                                         t.Type == SyntaxTokenType.PreprocessorNumber  ||
                                         t.Type == SyntaxTokenType.PragmaKeyword       ||
                                         t.Type == SyntaxTokenType.VersionKeyword),
                    Is.False, "Präprozessor-Token dürfen nicht mehr im flachen Token-Strom stehen.");

        // Die Direktive erscheint als strukturierte DirectiveTrivia (+ Zeilenende) in der Leading-Trivia von 'task'.
        var taskKeyword = First(tree, SyntaxTokenType.TaskKeyword);
        Assert.That(taskKeyword.LeadingTrivia.Select(t => t.Type),
                    Is.EqualTo(new[] { SyntaxTokenType.DirectiveTrivia, SyntaxTokenType.NewLine }));

        var directiveTrivia = taskKeyword.LeadingTrivia.First();
        Assert.That(directiveTrivia.HasStructure,        Is.True);
        Assert.That(directiveTrivia.GetStructure(),      Is.InstanceOf<BadDirectiveTriviaSyntax>());
        Assert.That(directiveTrivia.ToString(tree.SourceText), Is.EqualTo("#if DEBUG"));
    }

    // Vom Panic-Mode übersprungene Token (hier das '[]' in 'init [];') stehen nicht mehr als Skiped-Token
    // im flachen Strom, sondern sind ein einziges strukturiertes SkippedTokensTrivia-Stück — nach der
    // Roslyn-Zuordnungsregel in der Trailing-Trivia des vorangehenden Tokens ('init', gleiche Zeile).
    // Die übersprungenen Token liegen lokal am Knoten und behalten ihre Skiped-Klassifikation.
    [Test]
    public void SkippedTokensAreStructuredTriviaNotInTokenStream() {

        var source = "task A\r\n{\r\n    init [];\r\n}\r\n";
        var tree   = SyntaxTree.ParseText(source);

        // Die übersprungenen Klammern verbleiben nicht im flachen Strom.
        Assert.That(tree.Tokens.Any(t => t.Type == SyntaxTokenType.OpenBracket ||
                                         t.Type == SyntaxTokenType.CloseBracket),
                    Is.False, "Übersprungene Token dürfen nicht mehr im flachen Token-Strom stehen.");

        var initKeyword   = First(tree, SyntaxTokenType.InitKeyword);
        var skippedTrivia = initKeyword.TrailingTrivia.Single(t => t.Type == SyntaxTokenType.SkippedTokensTrivia);

        Assert.That(skippedTrivia.HasStructure,              Is.True);
        Assert.That(skippedTrivia.ToString(tree.SourceText), Is.EqualTo("[]"));
        Assert.That(skippedTrivia.GetStructure(),            Is.InstanceOf<SkippedTokensTriviaSyntax>());

        var skipped = (SkippedTokensTriviaSyntax) skippedTrivia.GetStructure();
        Assert.That(skipped.ChildTokens().Select(t => t.Type),
                    Is.EqualTo(new[] { SyntaxTokenType.OpenBracket, SyntaxTokenType.CloseBracket }));
        Assert.That(skipped.ChildTokens().Select(t => t.Classification),
                    Is.All.EqualTo(TextClassification.Skiped));
        Assert.That(skipped.ChildTokens().Select(t => t.Parent), Is.All.SameAs(skipped));

        // Auch über die Baum-Sicht erreichbar (wie Directives()).
        Assert.That(tree.SkippedTokens().Single(), Is.SameAs(skipped));
    }

    // Querschnitt: Bei trenner-freier Eingabe partitioniert das angehängte Modell die gesamte Trivia
    // lückenlos — Leading + Tokentext + Trailing aller signifikanten Token plus die Leading-Trivia des
    // EndOfFile ergeben den Quelltext wieder Zeichen für Zeichen.
    [Test]
    public void LeadingAndTrailingPartitionAllTriviaForSeparatorFreeSource() {

        var source = "// header\r\ntask A /* x */\r\n{\r\n    init I1;  // c\r\n\r\n    exit e1;\r\n    I1 --> e1;\r\n}\r\n";
        var tree   = SyntaxTree.ParseText(source);

        var sb = new StringBuilder();
        foreach (var token in tree.Tokens.Where(t => !IsTriviaType(t.Type) && t.Type != SyntaxTokenType.EndOfFile)) {
            Append(sb, token.LeadingTrivia, tree);
            sb.Append(token.ToString());
            Append(sb, token.TrailingTrivia, tree);
        }

        Append(sb, First(tree, SyntaxTokenType.EndOfFile).LeadingTrivia, tree);

        Assert.That(sb.ToString(), Is.EqualTo(source),
                    "Leading + Token + Trailing müssen die trenner-freie Eingabe lückenlos rekonstruieren.");
    }

    #region Infrastructure

    static SyntaxToken First(SyntaxTree tree, SyntaxTokenType type) {
        return tree.Tokens.First(t => t.Type == type);
    }

    static bool IsTriviaType(SyntaxTokenType type) {
        return type == SyntaxTokenType.Whitespace        ||
               type == SyntaxTokenType.NewLine           ||
               type == SyntaxTokenType.SingleLineComment ||
               type == SyntaxTokenType.MultiLineComment;
    }

    static string TriviaText(SyntaxTriviaList trivia, SyntaxTree tree) {
        var sb = new StringBuilder();
        Append(sb, trivia, tree);
        return sb.ToString();
    }

    static void Append(StringBuilder sb, IEnumerable<SyntaxTrivia> trivia, SyntaxTree tree) {
        foreach (var t in trivia) {
            sb.Append(t.ToString(tree.SourceText));
        }
    }

    #endregion
}
