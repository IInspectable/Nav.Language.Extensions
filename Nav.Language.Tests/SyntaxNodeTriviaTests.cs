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
/// Pinnt das Trivia-Verhalten der heutigen (ANTLR-basierten) Syntax-Schicht — der Bereich, der sich
/// beim Umbau auf strukturierte/angehängte Trivia ändern soll und deshalb vorher dicht festgehalten
/// werden muss. Abgedeckt: Leading/Trailing-Trivia-Extents (auch an den Dateirändern und in der
/// <c>onlyWhiteSpace</c>-Variante), die Parent-Zuordnung der Trivia-Token (heute alle am Root), der
/// SingleLineComment-/EOL-Split, mehrzeilige Kommentare, ein führendes BOM sowie Unicode-Zs-Whitespace.
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

    // Parent-Zuordnung: Trivia-Token (Whitespace, NewLine, Kommentare, Unknown) hängen heute allesamt
    // am Root (CodeGenerationUnitSyntax) — siehe PostprocessTokens ("hier evtl. den echten Parent...").
    // Genau dieses Verhalten festnageln, damit eine spätere Änderung als Diff sichtbar wird.
    [Test]
    public void AllTriviaTokensHangOnTheRoot() {

        var source = "// lead\r\ntask A /* mid */\r\n{\r\n    init I1;\r\n    exit e1;\r\n    I1 --> e1;\r\n}\r\n";
        var tree   = SyntaxTree.ParseText(source);

        var triviaTokens = tree.Tokens.Where(IsTriviaToken).ToList();

        Assert.That(triviaTokens.Select(t => t.Type), Does.Contain(SyntaxTokenType.SingleLineComment));
        Assert.That(triviaTokens.Select(t => t.Type), Does.Contain(SyntaxTokenType.MultiLineComment));

        foreach (var token in triviaTokens) {
            Assert.That(token.Parent, Is.SameAs(tree.Root),
                        $"Trivia-Token {token.Type} @ {token.Extent} sollte am Root hängen.");
        }
    }

    // SingleLineComment-/EOL-Split: '//c' + CRLF wird in einen Kommentar-Token OHNE '\n' (das '\r'
    // bleibt drin) plus ein separates NewLine-Token zerlegt. Hier zusätzlich Classification + Parent
    // festnageln (die reine Split-Mechanik je NL-Variante pinnt SyntaxNewLineTests).
    [Test]
    public void SingleLineCommentSplitClassificationAndParent() {

        var source = "//comment\r\ntask A\r\n{\r\n    init I1;\r\n    exit e1;\r\n    I1 --> e1;\r\n}\r\n";
        var tree   = SyntaxTree.ParseText(source);
        var tokens = NonEof(tree);

        var comment = tokens[0];
        Assert.That(comment.Type,           Is.EqualTo(SyntaxTokenType.SingleLineComment));
        Assert.That(comment.Classification, Is.EqualTo(TextClassification.Comment));
        Assert.That(comment.ToString(),     Is.EqualTo("//comment\r")); // '\r' bleibt im Kommentar
        Assert.That(comment.Parent,         Is.SameAs(tree.Root));

        var newLine = tokens[1];
        Assert.That(newLine.Type,           Is.EqualTo(SyntaxTokenType.NewLine));
        Assert.That(newLine.Classification, Is.EqualTo(TextClassification.Whitespace));
        Assert.That(newLine.ToString(),     Is.EqualTo("\n"));
        Assert.That(newLine.Parent,         Is.SameAs(tree.Root));
    }

    // MultiLineComment über mehrere Zeilen ist EIN Kommentar-Token; die eingebetteten Zeilenumbrüche
    // werden NICHT zu eigenen NewLine-Token (kein Split wie beim SingleLineComment).
    [Test]
    public void MultiLineCommentSpanningLinesIsOneToken() {

        var source = "/* a\r\n b\r\n c */task A\r\n{\r\n    init I1;\r\n    exit e1;\r\n    I1 --> e1;\r\n}\r\n";
        var tree   = SyntaxTree.ParseText(source);
        var tokens = NonEof(tree);

        var comment = tokens[0];
        Assert.That(comment.Type,           Is.EqualTo(SyntaxTokenType.MultiLineComment));
        Assert.That(comment.Classification, Is.EqualTo(TextClassification.Comment));
        Assert.That(comment.ToString(),     Is.EqualTo("/* a\r\n b\r\n c */"));
        Assert.That(comment.Parent,         Is.SameAs(tree.Root));

        // Direkt nach dem Kommentar folgt das 'task'-Keyword — kein zwischengeschobenes NewLine-Token.
        Assert.That(tokens[1].Type, Is.EqualTo(SyntaxTokenType.TaskKeyword));

        Assert.That(RoundTrip(tree), Is.EqualTo(source));
    }

    // Ein führendes BOM (U+FEFF) im geparsten Text wird heute als unerwartetes Zeichen behandelt:
    // ein Unknown-/Skiped-Token der Länge 1 am Root plus Nav0000. (File.ReadAllText entfernt das BOM
    // normalerweise vorab — dieser Test pinnt das Verhalten, falls es doch im Text landet.)
    [Test]
    public void LeadingByteOrderMarkBecomesUnexpectedCharacter() {

        var source = "\uFEFFtask A\r\n{\r\n    init I1;\r\n    exit e1;\r\n    I1 --> e1;\r\n}\r\n";
        var tree   = SyntaxTree.ParseText(source);
        var tokens = NonEof(tree);

        var bom = tokens[0];
        Assert.That(bom.Start,          Is.EqualTo(0));
        Assert.That(bom.Length,         Is.EqualTo(1));
        Assert.That(bom.Type,           Is.EqualTo(SyntaxTokenType.Unknown));
        Assert.That(bom.Classification, Is.EqualTo(TextClassification.Skiped));
        Assert.That(bom.Parent,         Is.SameAs(tree.Root));

        var bomDiagnostics = tree.Diagnostics.Where(d => d.Location.Start == 0).ToList();
        Assert.That(bomDiagnostics.Select(d => d.Descriptor.Id), Does.Contain("Nav0000"));

        Assert.That(RoundTrip(tree), Is.EqualTo(source));
    }

    // Unicode-Whitespace der Klasse Zs (z.B. NBSP, EM SPACE, IDEOGRAPHIC SPACE) ist ein gültiges
    // Trennzeichen: ein Whitespace-Trivia-Token am Root, keine Diagnose.
    static IEnumerable<TestCaseData> ZsWhitespaceCases() {
        yield return new TestCaseData("\u00A0").SetName("Zs NBSP (U+00A0)");
        yield return new TestCaseData("\u2003").SetName("Zs EM SPACE (U+2003)");
        yield return new TestCaseData("\u3000").SetName("Zs IDEOGRAPHIC SPACE (U+3000)");
    }

    [Test, TestCaseSource(nameof(ZsWhitespaceCases))]
    public void UnicodeZsIsWhitespaceTrivia(string zs) {

        var source = "task A\r\n{\r\n    init" + zs + "I1;\r\n    exit e1;\r\n    I1 --> e1;\r\n}\r\n";
        var tree   = SyntaxTree.ParseText(source);

        var zsToken = tree.Tokens.Single(t => t.ToString() == zs);
        Assert.That(zsToken.Type,           Is.EqualTo(SyntaxTokenType.Whitespace));
        Assert.That(zsToken.Classification, Is.EqualTo(TextClassification.Whitespace));
        Assert.That(zsToken.Parent,         Is.SameAs(tree.Root));

        Assert.That(tree.Diagnostics, Is.Empty, "Zs-Whitespace ist gültiges Trennzeichen und darf keine Diagnose erzeugen.");
        Assert.That(RoundTrip(tree),  Is.EqualTo(source));
    }

    #region Infrastructure

    static bool IsTriviaToken(SyntaxToken token) {
        return token.Type == SyntaxTokenType.Whitespace        ||
               token.Type == SyntaxTokenType.NewLine           ||
               token.Type == SyntaxTokenType.SingleLineComment ||
               token.Type == SyntaxTokenType.MultiLineComment  ||
               token.Type == SyntaxTokenType.Unknown;
    }

    // Alle Token außer dem abschließenden EndOfFile (Länge 0).
    static List<SyntaxToken> NonEof(SyntaxTree tree) {
        return tree.Tokens.Where(t => t.Type != SyntaxTokenType.EndOfFile).ToList();
    }

    static string RoundTrip(SyntaxTree tree) {
        var sb = new StringBuilder();
        foreach (var token in tree.Tokens) {
            sb.Append(token.ToString());
        }

        return sb.ToString();
    }

    #endregion
}