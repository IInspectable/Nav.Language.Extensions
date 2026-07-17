#region Using Directives

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Nav.Language.Tests;

/// <summary>
/// Pinnt das Trivia-Verhalten der Syntax-Schicht im angehängten Roslyn-Modell — Trivia (Whitespace,
/// Zeilenende, Kommentar) liegt ausschließlich als Leading/Trailing an den <see cref="SyntaxToken"/>,
/// nicht mehr als eigene Token im flachen Strom. Abgedeckt: Leading/Trailing-Trivia-Extents (auch an
/// den Dateirändern und in der <c>onlyWhiteSpace</c>-Variante), dass Kommentare als angehängte Trivia
/// (statt als Strom-Token) erscheinen, der SingleLineComment-/EOL-Split, mehrzeilige Kommentare, ein
/// führendes BOM (als übersprungenes <see cref="SyntaxTokenType.Unknown"/>-Token in der Skip-Trivia)
/// sowie Unicode-Zs-Whitespace.
/// Die reinen Zeilenende-Varianten (LF/CR/CRLF, NEL/LS/PS) pinnt zusätzlich
/// <see cref="SyntaxNewLineTests"/>.
/// </summary>
[TestFixture]
public class SyntaxNodeTriviaTests {

    [Test]
    public void GetLeadingTriviaExtentTests() {

        string source =
            @"//Foo
    task A;
    // Comment
    task B; //Comment
task C;
";
        Assert.That(Environment.NewLine.Length, Is.EqualTo(2), "Environment.NewLine");
        var ndb = Syntax.ParseNodeDeclarationBlock(source);

        var taskA = ndb.NodeDeclarations[0];

        Assert.That(taskA.GetLeadingTriviaExtent(),  Is.EqualTo(new TextExtent(0,  length: 11)));
        Assert.That(taskA.GetTrailingTriviaExtent(), Is.EqualTo(new TextExtent(18, length: 2))); // NewLine!!

        var taskB = ndb.NodeDeclarations[1];

        Assert.That(taskB.GetLeadingTriviaExtent(),  Is.EqualTo(new TextExtent(20, length: 4 + 2 + 14)));
        Assert.That(taskB.GetTrailingTriviaExtent(), Is.EqualTo(new TextExtent(47, length: 12))); // NewLine!!

        var taskC = ndb.NodeDeclarations[2];

        Assert.That(taskC.GetLeadingTriviaExtent(),  Is.EqualTo(new TextExtent(59, length: 0)));
        Assert.That(taskC.GetTrailingTriviaExtent(), Is.EqualTo(new TextExtent(66, length: 2))); // NewLine!!
    }

    [Test]
    public void GetLeadingTriviaExtentTests2() {

        string source = @" task A;    /* Comment*/    task B; /*Comment*/task C;
";
        var ndb = Syntax.ParseNodeDeclarationBlock(source);
        //
        var taskA = ndb.NodeDeclarations[0];

        Assert.That(taskA.GetLeadingTriviaExtent(),  Is.EqualTo(new TextExtent(0, length: 1)));
        Assert.That(taskA.GetTrailingTriviaExtent(), Is.EqualTo(new TextExtent(8, length: 20)));
        //
        var taskB = ndb.NodeDeclarations[1];
        //
        Assert.That(taskB.GetLeadingTriviaExtent(),  Is.EqualTo(new TextExtent(28, length: 0)));
        Assert.That(taskB.GetTrailingTriviaExtent(), Is.EqualTo(new TextExtent(35, length: 12)));
        //
        var taskC = ndb.NodeDeclarations[2];
        //
        Assert.That(taskC.GetLeadingTriviaExtent(),  Is.EqualTo(new TextExtent(47, length: 0)));
        Assert.That(taskC.GetTrailingTriviaExtent(), Is.EqualTo(new TextExtent(54, length: 2))); // NewLine!!
    }

    [Test]
    public void GetLeadingTriviaExtentTestsOnlyWhitespace() {

        string source =
            @"//Foo
    task A;
    // Comment
    task B; //Comment
task C;
";
        var ndb = Syntax.ParseNodeDeclarationBlock(source);

        var taskA = ndb.NodeDeclarations[0];

        Assert.That(taskA.GetLeadingTriviaExtent(onlyWhiteSpace: true),  Is.EqualTo(new TextExtent(7,  length: 4)));
        Assert.That(taskA.GetTrailingTriviaExtent(onlyWhiteSpace: true), Is.EqualTo(new TextExtent(18, length: 2))); // NewLine!!

        var taskB = ndb.NodeDeclarations[1];

        Assert.That(taskB.GetLeadingTriviaExtent(onlyWhiteSpace: true),  Is.EqualTo(new TextExtent(36, length: 4)));
        Assert.That(taskB.GetTrailingTriviaExtent(onlyWhiteSpace: true), Is.EqualTo(new TextExtent(47, length: 1)));

        var taskC = ndb.NodeDeclarations[2];

        Assert.That(taskC.GetLeadingTriviaExtent(onlyWhiteSpace: true),  Is.EqualTo(new TextExtent(59, length: 0)));
        Assert.That(taskC.GetTrailingTriviaExtent(onlyWhiteSpace: true), Is.EqualTo(new TextExtent(66, length: 2))); // NewLine!!
    }

    [Test]
    public void GetLeadingTriviaExtentTestsOnlyWhitespace2() {

        string source = @" task A;    /* Comment*/    task B; /*Comment*/task C;
";
        var ndb = Syntax.ParseNodeDeclarationBlock(source);
        //
        var taskA = ndb.NodeDeclarations[0];

        Assert.That(taskA.GetLeadingTriviaExtent(onlyWhiteSpace: true),  Is.EqualTo(new TextExtent(0, length: 1)));
        Assert.That(taskA.GetTrailingTriviaExtent(onlyWhiteSpace: true), Is.EqualTo(new TextExtent(8, length: 4)));
        //
        var taskB = ndb.NodeDeclarations[1];
        //
        Assert.That(taskB.GetLeadingTriviaExtent(onlyWhiteSpace: true),  Is.EqualTo(new TextExtent(28, length: 0)));
        Assert.That(taskB.GetTrailingTriviaExtent(onlyWhiteSpace: true), Is.EqualTo(new TextExtent(35, length: 1)));
        //
        var taskC = ndb.NodeDeclarations[2];
        //
        Assert.That(taskC.GetLeadingTriviaExtent(onlyWhiteSpace: true),  Is.EqualTo(new TextExtent(47, length: 0)));
        Assert.That(taskC.GetTrailingTriviaExtent(onlyWhiteSpace: true), Is.EqualTo(new TextExtent(54, length: 2))); // NewLine!!
    }

    // Ein mehrzeiliger Kommentar direkt über einem Knoten: im onlyWhiteSpace-Modus begrenzt er den
    // Leading-Ausschnitt wie jeder Kommentar (nur die Einrückung der Knotenzeile zählt), während er ohne
    // onlyWhiteSpace mitgenommen wird. Damit verhalten sich ein- und mehrzeilige Kommentare als Grenze
    // gleich — abgeleitet aus der angehängten Token-Trivia (Roslyn-Modell).
    [Test]
    public void MultiLineCommentBoundsOnlyWhiteSpaceLeading() {

        string source = "task A;\r\n/* multi\r\nline */\r\n    task B;\r\n";
        var    ndb    = Syntax.ParseNodeDeclarationBlock(source);

        var taskB = ndb.NodeDeclarations[1];

        // Ohne onlyWhiteSpace gehört der Kommentar (samt darüberliegender Zeilen) zur Leading-Trivia.
        var leadingFull = taskB.GetLeadingTriviaExtent();
        Assert.That(source.Substring(leadingFull.Start, leadingFull.Length), Does.Contain("/* multi"));

        // Mit onlyWhiteSpace begrenzt der Kommentar den Ausschnitt — nur die Einrückung der Knotenzeile bleibt.
        var leadingWs = taskB.GetLeadingTriviaExtent(onlyWhiteSpace: true);
        Assert.That(source.Substring(leadingWs.Start, leadingWs.Length), Is.EqualTo("    "));
    }

    // An den Dateirändern (Vollparse statt Sub-Regel): führende Trivia des ersten Knotens reicht bis
    // zum Dateianfang (inkl. Header-Kommentar), nachfolgende Trivia des letzten Knotens bis ans EOF.
    [Test]
    public void TriviaExtentsReachFileBoundaries() {

        var source = "// header\r\ntask A\r\n{\r\n    init I1;\r\n    exit e1;\r\n    I1 --> e1;\r\n}\r\n";
        var tree   = SyntaxTree.ParseText(source);

        var taskDefinition = tree.Root.DescendantNodes<TaskDefinitionSyntax>().Single();

        var leading = taskDefinition.GetLeadingTriviaExtent();
        Assert.That(leading.Start,             Is.EqualTo(0));
        Assert.That(source.Substring(leading.Start, leading.Length), Does.Contain("// header"));

        Assert.That(taskDefinition.GetTrailingTriviaExtent().End, Is.EqualTo(source.Length));
    }

    // Whitespace/Zeilenende/Kommentar liegen nicht mehr als eigene Token im flachen Strom, sondern
    // ausschließlich als angehängte Trivia. Hier festnageln: die Kommentare sind über die Trivia-Sicht
    // am Baum erreichbar und tauchen NICHT mehr im Token-Strom auf.
    [Test]
    public void CommentsAreAttachedTriviaNotStreamTokens() {

        var source = "// lead\r\ntask A /* mid */\r\n{\r\n    init I1;\r\n    exit e1;\r\n    I1 --> e1;\r\n}\r\n";
        var tree   = SyntaxTree.ParseText(source);

        var triviaTypes = tree.DescendantTrivia().Select(t => t.Type).ToList();
        Assert.That(triviaTypes, Does.Contain(SyntaxTokenType.SingleLineComment));
        Assert.That(triviaTypes, Does.Contain(SyntaxTokenType.MultiLineComment));

        Assert.That(tree.Tokens.Any(t => SyntaxFacts.IsTrivia(t.Type)), Is.False,
                    "Whitespace/Zeilenende/Kommentar dürfen nicht mehr im flachen Token-Strom stehen.");
    }

    // SingleLineComment-/EOL-Split: '//c' + CRLF wird in eine Kommentar-Trivia OHNE '\n' (das '\r'
    // bleibt drin) plus eine separate NewLine-Trivia zerlegt — hier an der angehängten Leading-Trivia
    // des ersten signifikanten Tokens festgenagelt (die reine Split-Mechanik je NL-Variante pinnt
    // SyntaxNewLineTests).
    [Test]
    public void SingleLineCommentSplitInLeadingTrivia() {

        var source = "//comment\r\ntask A\r\n{\r\n    init I1;\r\n    exit e1;\r\n    I1 --> e1;\r\n}\r\n";
        var tree   = SyntaxTree.ParseText(source);

        var firstToken = NonEof(tree)[0];
        Assert.That(firstToken.Type, Is.EqualTo(SyntaxTokenType.TaskKeyword));

        var leading = firstToken.LeadingTrivia;
        Assert.That(leading.Length, Is.EqualTo(2));

        Assert.That(leading[0].Type,                      Is.EqualTo(SyntaxTokenType.SingleLineComment));
        Assert.That(leading[0].ToString(tree.SourceText), Is.EqualTo("//comment\r")); // '\r' bleibt im Kommentar

        Assert.That(leading[1].Type,                      Is.EqualTo(SyntaxTokenType.NewLine));
        Assert.That(leading[1].ToString(tree.SourceText), Is.EqualTo("\n"));
    }

    // MultiLineComment über mehrere Zeilen ist EINE Kommentar-Trivia; die eingebetteten Zeilenumbrüche
    // werden NICHT zu eigenen NewLine-Trivia (kein Split wie beim SingleLineComment).
    [Test]
    public void MultiLineCommentSpanningLinesIsOneTriviaPiece() {

        var source = "/* a\r\n b\r\n c */task A\r\n{\r\n    init I1;\r\n    exit e1;\r\n    I1 --> e1;\r\n}\r\n";
        var tree   = SyntaxTree.ParseText(source);

        var firstToken = NonEof(tree)[0];
        Assert.That(firstToken.Type, Is.EqualTo(SyntaxTokenType.TaskKeyword));

        var leading = firstToken.LeadingTrivia;
        Assert.That(leading.Length, Is.EqualTo(1));
        Assert.That(leading[0].Type,                      Is.EqualTo(SyntaxTokenType.MultiLineComment));
        Assert.That(leading[0].ToString(tree.SourceText), Is.EqualTo("/* a\r\n b\r\n c */"));

        Assert.That(RoundTrip(tree), Is.EqualTo(source));
    }

    // Ein führendes BOM (U+FEFF) im geparsten Text wird heute als unerwartetes Zeichen behandelt: ein
    // übersprungenes Unknown-Token in der Skip-Trivia des ersten Strom-Tokens plus Nav0000.
    // (File.ReadAllText entfernt das BOM normalerweise vorab — dieser Test pinnt das Verhalten, falls
    // es doch im Text landet.)
    [Test]
    public void LeadingByteOrderMarkBecomesUnexpectedCharacter() {

        var source = "\uFEFFtask A\r\n{\r\n    init I1;\r\n    exit e1;\r\n    I1 --> e1;\r\n}\r\n";
        var tree   = SyntaxTree.ParseText(source);
        var tokens = NonEof(tree);

        // Das BOM steht nicht mehr im flachen Strom — das erste Strom-Token ist 'task'.
        Assert.That(tokens[0].Type, Is.EqualTo(SyntaxTokenType.TaskKeyword));

        var skippedTrivia = tokens[0].LeadingTrivia.Single(t => t.Type == SyntaxTokenType.SkippedTokensTrivia);
        Assert.That(skippedTrivia.Extent,       Is.EqualTo(new TextExtent(0, length: 1)));
        Assert.That(skippedTrivia.HasStructure, Is.True);

        var bom = ((SkippedTokensTriviaSyntax) skippedTrivia.GetStructure()).ChildTokens().Single();
        Assert.That(bom.Start,          Is.EqualTo(0));
        Assert.That(bom.Length,         Is.EqualTo(1));
        Assert.That(bom.Type,           Is.EqualTo(SyntaxTokenType.Unknown));
        Assert.That(bom.Classification, Is.EqualTo(TextClassification.Skiped));
        Assert.That(bom.Parent,         Is.InstanceOf<SkippedTokensTriviaSyntax>());

        var bomDiagnostics = tree.Diagnostics.Where(d => d.Location.Start == 0).ToList();
        Assert.That(bomDiagnostics.Select(d => d.Descriptor.Id), Does.Contain("Nav0000"));

        Assert.That(RoundTrip(tree), Is.EqualTo(source));
    }

    // Unicode-Whitespace der Klasse Zs (z.B. NBSP, EM SPACE, IDEOGRAPHIC SPACE) ist ein gültiges
    // Trennzeichen: eine Whitespace-Trivia, keine Diagnose.
    static IEnumerable<TestCaseData> ZsWhitespaceCases() {
        yield return new TestCaseData("\u00A0").SetName("Zs NBSP (U+00A0)");
        yield return new TestCaseData("\u2003").SetName("Zs EM SPACE (U+2003)");
        yield return new TestCaseData("\u3000").SetName("Zs IDEOGRAPHIC SPACE (U+3000)");
    }

    [Test, TestCaseSource(nameof(ZsWhitespaceCases))]
    public void UnicodeZsIsWhitespaceTrivia(string zs) {

        var source = "task A\r\n{\r\n    init" + zs + "I1;\r\n    exit e1;\r\n    I1 --> e1;\r\n}\r\n";
        var tree   = SyntaxTree.ParseText(source);

        var zsTrivia = tree.DescendantTrivia().Single(t => t.ToString(tree.SourceText) == zs);
        Assert.That(zsTrivia.Type, Is.EqualTo(SyntaxTokenType.Whitespace));

        Assert.That(tree.Diagnostics, Is.Empty, "Zs-Whitespace ist gültiges Trennzeichen und darf keine Diagnose erzeugen.");
        Assert.That(RoundTrip(tree),  Is.EqualTo(source));
    }

    #region Infrastructure

    // Alle Token außer dem abschließenden EndOfFile (Länge 0).
    static List<SyntaxToken> NonEof(SyntaxTree tree) {
        return tree.Tokens.Where(t => t.Type != SyntaxTokenType.EndOfFile).ToList();
    }

    // Trivia liegt nicht mehr als eigenes Token im Strom — der lückenlose Round-Trip rekonstruiert je
    // Strom-Token dessen Leading-Trivia, den eigenen Text und die Trailing-Trivia.
    static string RoundTrip(SyntaxTree tree) {
        var sb = new StringBuilder();
        foreach (var token in tree.Tokens) {

            foreach (var trivia in token.LeadingTrivia) {
                sb.Append(trivia.ToString(tree.SourceText));
            }

            sb.Append(token.ToString());

            foreach (var trivia in token.TrailingTrivia) {
                sb.Append(trivia.ToString(tree.SourceText));
            }
        }

        return sb.ToString();
    }

    #endregion
}