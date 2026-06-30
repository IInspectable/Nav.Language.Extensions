using System.Linq;
using NUnit.Framework;
using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.Text;

namespace Nav.Language.Tests; 

[TestFixture]
public class SyntaxTokenTests {

    [Test]
    public void TestFindAtPositionResolvesGapsToMissing() {
        // Trivia (hier der Whitespace) liegt nicht mehr als eigenes Token im flachen Strom — der exakte
        // FindAtPosition liefert daher an einer Whitespace-/EOF-Position Missing, nur auf dem eigenen
        // Extent eines signifikanten Tokens dieses Token. Positionen: 0:' ' 1:'[' 2-6:'using' 7:' ' 8:'U' 9:']'.
        string usingText = " [using U]";

        var syntaxTree = Syntax.ParseCodeUsingDeclaration(usingText).SyntaxTree;

        Assert.That(syntaxTree.Diagnostics.Length, Is.EqualTo(0));

        Assert.That(syntaxTree.Tokens.FindAtPosition(-1).IsMissing, Is.True);
        Assert.That(syntaxTree.Tokens.FindAtPosition(0).IsMissing,  Is.True); // führender Whitespace
        Assert.That(syntaxTree.Tokens.FindAtPosition(1).Type,       Is.EqualTo(SyntaxTokenType.OpenBracket));
        Assert.That(syntaxTree.Tokens.FindAtPosition(2).Type,       Is.EqualTo(SyntaxTokenType.UsingKeyword));
        Assert.That(syntaxTree.Tokens.FindAtPosition(6).Type,       Is.EqualTo(SyntaxTokenType.UsingKeyword));
        Assert.That(syntaxTree.Tokens.FindAtPosition(7).IsMissing,  Is.True); // Whitespace zwischen 'using' und 'U'
        Assert.That(syntaxTree.Tokens.FindAtPosition(8).Type,       Is.EqualTo(SyntaxTokenType.Identifier));
        Assert.That(syntaxTree.Tokens.FindAtPosition(9).Type,       Is.EqualTo(SyntaxTokenType.CloseBracket));
        Assert.That(syntaxTree.Tokens.FindAtPosition(10).IsMissing, Is.True); // EOF-Position (nullbreit)
    }

    [Test]
    public void TestFindAtPositionWithTrailingWhitespaceIsMissing() {
        string usingText = " [using U] "; // zusätzlicher abschließender Whitespace an Position 10

        var syntaxTree = Syntax.ParseCodeUsingDeclaration(usingText).SyntaxTree;

        Assert.That(syntaxTree.Diagnostics.Length, Is.EqualTo(0));

        Assert.That(syntaxTree.Tokens.FindAtPosition(9).Type,       Is.EqualTo(SyntaxTokenType.CloseBracket));
        Assert.That(syntaxTree.Tokens.FindAtPosition(10).IsMissing, Is.True); // abschließender Whitespace
        Assert.That(syntaxTree.Tokens.FindAtPosition(11).IsMissing, Is.True); // EOF-Position
    }

    [Test]
    public void TestFindAtExtent() {
        // Ohne führenden Whitespace, damit Position 0 ein Token-Start ist. Token-Extents:
        // 'task'[0,4) 'Foo'[5,8) '{'[9,10) 'init'[11,15) 'I'[16,17) ';'[17,18) '}'[19,20). Trivia (die
        // Leerstellen) liegt nicht mehr im Strom — nur vollständig im Extent enthaltene Token zählen.
        string taskDef = "task Foo { init I; } ";

        var syntaxTree = Syntax.ParseTaskDefinition(taskDef).SyntaxTree;

        Assert.That(syntaxTree.Diagnostics.Length, Is.EqualTo(0));

        AssertTokens(syntaxTree.Tokens, 0, 1);                                            // Teil von 'task' → nichts vollständig drin
        AssertTokens(syntaxTree.Tokens, 0, 3);                                            // weiterhin nur Teil von 'task'
        AssertTokens(syntaxTree.Tokens, 0, 4, SyntaxTokenType.TaskKeyword);               // exakt 'task'
        AssertTokens(syntaxTree.Tokens, 0, 5, SyntaxTokenType.TaskKeyword);               // 'Foo' ragt über die Grenze → nicht drin
        AssertTokens(syntaxTree.Tokens, 0, 8, SyntaxTokenType.TaskKeyword, SyntaxTokenType.Identifier); // 'task' + 'Foo'
        AssertTokens(syntaxTree.Tokens, 5, 8, SyntaxTokenType.Identifier);                // exakt 'Foo'
    }

    [Test]
    public void TestFindAtExtentIncludeOverlapping() {
        string taskDef = "task Foo { init I; } ";

        var syntaxTree = Syntax.ParseTaskDefinition(taskDef).SyntaxTree;

        Assert.That(syntaxTree.Diagnostics.Length, Is.EqualTo(0));

        AssertTokens(syntaxTree.Tokens, 0, 0, true, SyntaxTokenType.TaskKeyword);         // Punktsuche am Token-Start
        AssertTokens(syntaxTree.Tokens, 1, 2, true, SyntaxTokenType.TaskKeyword);         // mitten in 'task'
        AssertTokens(syntaxTree.Tokens, 2, 6, true, SyntaxTokenType.TaskKeyword, SyntaxTokenType.Identifier); // überlappt 'task' und 'Foo'
        AssertTokens(syntaxTree.Tokens, 5, 8, true, SyntaxTokenType.Identifier);          // exakt 'Foo'
        AssertTokens(syntaxTree.Tokens, 0, 10, true, SyntaxTokenType.TaskKeyword, SyntaxTokenType.Identifier, SyntaxTokenType.OpenBrace);
    }

    void AssertTokens(SyntaxTokenList tokenList, int start, int end, params SyntaxTokenType[] expectedTypes) {
        AssertTokens(tokenList, start, end, false, expectedTypes);
    }

    void AssertTokens(SyntaxTokenList tokenList, int start, int end, bool includeOverlapping, params SyntaxTokenType[] expectedTypes) {
        var extent = TextExtent.FromBounds(start, end);
        var tokens = tokenList[extent, includeOverlapping].ToList();

        Assert.That(tokens.Count, Is.EqualTo(expectedTypes.Length));
        for (int i = tokens.Count - 1; i >= 0; i--) {
            var token = tokens[i];
            Assert.That(token.Type, Is.EqualTo(expectedTypes[i]));
        }
    }

    // Dass ein Kommentar das nachfolgende Zeilenende NICHT verschluckt (es bleibt ein eigenes NewLine),
    // ist reines Lexer-Verhalten — seit die Trivia nicht mehr im flachen Parser-Strom liegt, direkt
    // über NavLexer.Lex geprüft (die rohe Lex-Folge enthält Whitespace/Zeilenende/Kommentar).
    [Test]
    public void TestNewLineAfterSingleLineComments() {
        string source =
            @"//Comment
task B;
";
        var tokens = NavLexer.Lex(source);

        Assert.That(tokens[0].Type, Is.EqualTo(SyntaxTokenType.SingleLineComment));
        Assert.That(tokens[1].Type, Is.EqualTo(SyntaxTokenType.NewLine));
        Assert.That(tokens[2].Type, Is.EqualTo(SyntaxTokenType.TaskKeyword));
    }

    [Test]
    public void TestNewLineAfterSingleLineComments2() {
        string source =
            @"task A;
                 //Comment
task B;
";
        var tokens = NavLexer.Lex(source);

        Assert.That(tokens[0].Type, Is.EqualTo(SyntaxTokenType.TaskKeyword));
        Assert.That(tokens[1].Type, Is.EqualTo(SyntaxTokenType.Whitespace));
        Assert.That(tokens[2].Type, Is.EqualTo(SyntaxTokenType.Identifier));
        Assert.That(tokens[3].Type, Is.EqualTo(SyntaxTokenType.Semicolon));
        Assert.That(tokens[4].Type, Is.EqualTo(SyntaxTokenType.NewLine));
        Assert.That(tokens[5].Type, Is.EqualTo(SyntaxTokenType.Whitespace));
        Assert.That(tokens[6].Type, Is.EqualTo(SyntaxTokenType.SingleLineComment));
        Assert.That(tokens[7].Type, Is.EqualTo(SyntaxTokenType.NewLine));
    }

    [Test]
    public void TestNewLineAfterMultiLineComments() {
        string source =
            @"/*Comment*/
task B;
";
        var tokens = NavLexer.Lex(source);

        Assert.That(tokens[0].Type, Is.EqualTo(SyntaxTokenType.MultiLineComment));
        Assert.That(tokens[1].Type, Is.EqualTo(SyntaxTokenType.NewLine));
        Assert.That(tokens[2].Type, Is.EqualTo(SyntaxTokenType.TaskKeyword));
    }

    [Test]
    public void TestEmptyToken() {
        var empty = SyntaxToken.Empty;

        Assert.That(empty.IsMissing       , Is.True);
        Assert.That(empty.Extent.IsEmpty  , Is.True);
        Assert.That(empty.Extent.IsMissing, Is.False);
        Assert.That(empty.Parent          , Is.Null);
        Assert.That(empty.SyntaxTree      , Is.Null);

        Assert.That(empty.Type            , Is.EqualTo(SyntaxTokenType.Unknown));
        Assert.That(empty.Classification  , Is.EqualTo(TextClassification.Unknown));
    }

    [Test]
    public void TestMissingToken() {
        var missing = SyntaxToken.Missing;

        Assert.That(missing.IsMissing       , Is.True);
        Assert.That(missing.Extent.IsEmpty  , Is.True);
        Assert.That(missing.Extent.IsMissing, Is.True);
        Assert.That(missing.Parent          , Is.Null);
        Assert.That(missing.SyntaxTree      , Is.Null);

        Assert.That(missing.Type            , Is.EqualTo(SyntaxTokenType.Unknown));
        Assert.That(missing.Classification  , Is.EqualTo(TextClassification.Unknown));
    }

    [Test]
    public void TestNextTokenOnMissingToken() {
        var missing = SyntaxToken.Missing;

        var next = missing.NextToken();

        Assert.That(next.IsMissing, Is.True);
    }

    [Test]
    public void TestPreviousTokenOnMissingToken() {
        var missing = SyntaxToken.Missing;

        var next = missing.PreviousToken();

        Assert.That(next.IsMissing, Is.True);
    }

    [Test]
    public void TestCommentAtEndOfFile() {
        string usingText = " [using U]" +
                           "//Comment";

        var syntaxTree = Syntax.ParseCodeUsingDeclaration(usingText).SyntaxTree;
        var tokens     = syntaxTree.Tokens;
        Assert.That(tokens[tokens.Count - 1].Type, Is.EqualTo(SyntaxTokenType.EndOfFile));
    }

    [Test]
    public void TestEndOfFile() {
        string usingText = " [using U]";

        var syntaxTree = Syntax.ParseCodeUsingDeclaration(usingText).SyntaxTree;
        var tokens     = syntaxTree.Tokens;
        Assert.That(tokens[tokens.Count - 1].Type, Is.EqualTo(SyntaxTokenType.EndOfFile));
    }

    [Test]
    public void TestEndOfFileOnEmptyString() {
        string usingText = "";

        var syntaxTree = Syntax.ParseCodeUsingDeclaration(usingText).SyntaxTree;
        var tokens     = syntaxTree.Tokens;
        Assert.That(tokens.Count,                  Is.EqualTo(1));
        Assert.That(tokens[tokens.Count - 1].Type, Is.EqualTo(SyntaxTokenType.EndOfFile));
    }

    [Test]
    public void TestEndOfFileOnSpace() {
        string usingText = " ";

        var syntaxTree = Syntax.ParseCodeUsingDeclaration(usingText).SyntaxTree;
        var tokens     = syntaxTree.Tokens;
        Assert.That(tokens[tokens.Count - 1].Type, Is.EqualTo(SyntaxTokenType.EndOfFile));
    }

    #region FindToken (Roslyn-Owning-Semantik) vs. FindAtPosition (exakt)

    [Test]
    public void FindToken_OnSignificantToken_ReturnsThatToken() {
        const string src = "task A   // c\n{\n    init I1;\n}\n";

        var tree  = Syntax.ParseTaskDefinition(src).SyntaxTree;
        var aPos  = src.IndexOf("A", System.StringComparison.Ordinal);
        var token = tree.Root.FindToken(aPos);

        Assert.That(Text(tree, token), Is.EqualTo("A"));
        // Auf einer Nicht-Trivia-Position fällt Owning mit dem exakten Lookup zusammen.
        Assert.That(token.Extent, Is.EqualTo(tree.Tokens.FindAtPosition(aPos).Extent));
    }

    [Test]
    public void FindToken_InTrailingTrivia_ReturnsOwningToken() {
        const string src = "task A   // c\n{\n    init I1;\n}\n";

        var tree       = Syntax.ParseTaskDefinition(src).SyntaxTree;
        var spacePos   = src.IndexOf("   //", System.StringComparison.Ordinal) + 1; // im Whitespace nach 'A'
        var commentPos = src.IndexOf("// c",  System.StringComparison.Ordinal) + 1; // im Zeilenkommentar

        Assert.That(Text(tree, tree.Root.FindToken(spacePos)),   Is.EqualTo("A"), "Whitespace nach 'A' gehört zu 'A'");
        Assert.That(Text(tree, tree.Root.FindToken(commentPos)), Is.EqualTo("A"), "Trailing-Kommentar gehört zu 'A'");
    }

    [Test]
    public void FindToken_InLeadingIndentation_ReturnsFollowingToken() {
        const string src = "task A\n{\n    init I1;\n}\n";

        var tree      = Syntax.ParseTaskDefinition(src).SyntaxTree;
        var indentPos = src.IndexOf("    init", System.StringComparison.Ordinal) + 2; // in der Einrückung vor 'init'
        var initToken = tree.Root.FindToken(src.IndexOf("init", System.StringComparison.Ordinal));

        Assert.That(tree.Root.FindToken(indentPos).Extent, Is.EqualTo(initToken.Extent));
        Assert.That(Text(tree, tree.Root.FindToken(indentPos)), Is.EqualTo("init"));
    }

    [Test]
    public void FindToken_OutOfRange_ReturnsMissing() {
        const string src = "task A\n{\n    init I1;\n}\n";

        var tree = Syntax.ParseTaskDefinition(src).SyntaxTree;

        Assert.That(tree.Root.FindToken(-1).IsMissing,             Is.True);
        Assert.That(tree.Root.FindToken(src.Length + 5).IsMissing, Is.True);
    }

    static string Text(SyntaxTree tree, SyntaxToken token) => tree.SourceText.Substring(token.Extent);

    #endregion
}