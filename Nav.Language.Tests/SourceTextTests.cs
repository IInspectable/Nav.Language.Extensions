﻿using System;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.Text;

namespace Nav.Language.Tests; 

[TestFixture]
public class SourceTextTests {

    [Test]
    public void TestEmpty() {

        SourceText st = SourceText.Empty;

        Assert.That(st.Text,            Is.EqualTo(string.Empty));
        Assert.That(st.Length,          Is.EqualTo(0));
        Assert.That(st.FileInfo,        Is.Null);
        Assert.That(st.TextLines.Count, Is.EqualTo(1));

        var tl = st.GetTextLineAtPosition(0);

        Assert.That(tl.ToString(), Is.EqualTo(string.Empty));

    }

    [Test]
    public void TestSingleLine() {
        const string testText = "hello There!";

        SourceText st = SourceText.From(testText);

        Assert.That(st.Text,            Is.EqualTo(testText));
        Assert.That(st.Length,          Is.EqualTo(testText.Length));
        Assert.That(st.FileInfo,        Is.Null);
        Assert.That(st.TextLines.Count, Is.EqualTo(1));

        var tl = st.GetTextLineAtPosition(0);

        Assert.That(tl.ToString(),      Is.EqualTo(testText));
        Assert.That(tl.Span.ToString(), Is.EqualTo(testText));

    }

    [Test]
    public void TestLineAndEmptyLine() {
        const string testText = "hello There!\r\n";

        SourceText st = SourceText.From(testText);

        Assert.That(st.Text,            Is.EqualTo(testText));
        Assert.That(st.Length,          Is.EqualTo(testText.Length));
        Assert.That(st.FileInfo,        Is.Null);
        Assert.That(st.TextLines.Count, Is.EqualTo(2));

        Assert.That(st.TextLines[0].ToString(),      Is.EqualTo("hello There!\r\n"));
        Assert.That(st.TextLines[0].Span.ToString(), Is.EqualTo("hello There!\r\n"));
        Assert.That(st.TextLines[1].ToString(),      Is.EqualTo(""));
    }

    [Test]
    public void TextLinesTest() {
        var syntaxTree = SyntaxTree.ParseText(Resources.LargeNav);

        int expectedLine = 0;
        int currentEnd   = 0;

        foreach (var lineExtent in syntaxTree.SourceText.TextLines) {
            // Keine Zeilensprünge
            Assert.That(lineExtent.Line, Is.EqualTo(expectedLine));
            // Lückenlosigkeit
            Assert.That(lineExtent.Start, Is.EqualTo(currentEnd));

            expectedLine++;
            currentEnd = lineExtent.End;
        }

        Assert.That(currentEnd, Is.EqualTo(Resources.LargeNav.Length));
    }

    [Test]
    public void TestGetLoationInTextLine() {

        const string testText = "Hello There!\r\nNext Line";

        SourceText st = SourceText.From(testText);

        // "There"
        var line1 = st.TextLines[0];
        var loc1  = line1.GetLocation(6, 5);
        var text1 = st.Substring(loc1.Start, loc1.Length);

        Assert.That(loc1.StartLinePosition.Line,      Is.EqualTo(0));
        Assert.That(loc1.StartLinePosition.Character, Is.EqualTo(6));
        Assert.That(loc1.EndLinePosition.Line,        Is.EqualTo(0));
        Assert.That(loc1.EndLinePosition.Character,   Is.EqualTo(11));
        Assert.That(text1,                            Is.EqualTo("There"));

        // "Next"
        var line2 = st.TextLines[1];
        var loc2  = line2.GetLocation(0, 4);
        var text2 = st.Substring(loc2.Start, loc2.Length);

        Assert.That(loc2.StartLinePosition.Line,      Is.EqualTo(1));
        Assert.That(loc2.StartLinePosition.Character, Is.EqualTo(0));
        Assert.That(loc2.EndLinePosition.Line,        Is.EqualTo(1));
        Assert.That(loc2.EndLinePosition.Character,   Is.EqualTo(4));
        Assert.That(text2,                            Is.EqualTo("Next"));
    }

    [Test]
    public void SliceFromLineStartToPosition() {
        const string testText = "Hello There!\r\n" +
                                "Next Line";

        SourceText st = SourceText.From(testText);

        var position = testText.IndexOf("Line", StringComparison.Ordinal);
        var sliceEnd = st.SliceFromPositionToLineEnd(position);
        Assert.That(sliceEnd.ToString(), Is.EqualTo("Line"));
    }

    [Test]
    public void SliceFromPositionToLineEnd() {
        const string testText = "Hello There!\r\n" +
                                "Next Line";

        SourceText st = SourceText.From(testText);

        var position = testText.IndexOf("Line", StringComparison.Ordinal);
        var sliceEnd = st.SliceFromPositionToLineEnd(position);
        Assert.That(sliceEnd.ToString(), Is.EqualTo("Line"));
    }

    [Test]
    public void TextLineSlice() {
        const string testText = "Hello There!\r\n" +
                                "Next Line";

        SourceText st = SourceText.From(testText);

        var tl2   = st.TextLines[1];
        var slice = tl2.Slice(charPositionInLine: 5, length: 4);
        Assert.That(slice.ToString(), Is.EqualTo("Line"));
    }

    [Test]
    public void GetSignificantColumn1() {
        const string testText = "Hello There!\r\n" +
                                "\t Foo";

        SourceText st = SourceText.From(testText);

        var tl2 = st.TextLines[1];

        var col = tl2.GetSignificantColumn(tabSize: 4);
        Assert.That(col, Is.EqualTo(4 + 1));
    }

    [Test]
    public void GetSignificantColumn2() {
        const string testText = "Hello There!\r\n" +
                                "\t    Foo";

        SourceText st = SourceText.From(testText);

        var tl2 = st.TextLines[1];

        var col = tl2.GetSignificantColumn(tabSize: 4);
        Assert.That(col, Is.EqualTo(4 + 4));
    }

    [Test]
    public void GetSignificantColumn3() {
        const string testText = "Hello There!\r\n" +
                                " \t Foo";

        SourceText st = SourceText.From(testText);

        var tl2 = st.TextLines[1];

        var col = tl2.GetSignificantColumn(tabSize: 4);
        Assert.That(col, Is.EqualTo(0 + 4 + 1));
    }

    [Test]
    public void GetSignificantColumnEmptyLine() {
        const string testText = "Hello There!\r\n" +
                                "";

        SourceText st = SourceText.From(testText);

        var tl2 = st.TextLines[1];

        var col = tl2.GetSignificantColumn(tabSize: 4);
        Assert.That(col, Is.EqualTo(Int32.MaxValue));
    }

    [Test]
    public void GetSignificantColumnWhiteSpaceLine() {
        const string testText = "Hello There!\r\n" +
                                "   ";

        SourceText st = SourceText.From(testText);

        var tl2 = st.TextLines[1];

        var col = tl2.GetSignificantColumn(tabSize: 4);
        Assert.That(col, Is.EqualTo(Int32.MaxValue));
    }

    [Test]
    public void GetIndentAsSpaces() {
        const string testText = "Hello There!\r\n" +
                                " \t Foo";

        SourceText st = SourceText.From(testText);

        var tl2 = st.TextLines[1];

        var spaces = tl2.GetIndentAsSpaces(tabSize: 4);
        Assert.That(spaces, Is.EqualTo("     "));
    }

    [Test]
    public void GetColumnForOffset1() {
        const string testText = "Hello There!\r\n" +
                                "\tFoo";

        SourceText st = SourceText.From(testText);

        var tl2 = st.TextLines[1];

        var col = tl2.GetColumnForOffset(tabSize: 4, charPositionInLine: 1);
        Assert.That(col, Is.EqualTo(4));
    }

    [Test]
    public void GetColumnForOffset2() {
        const string testText = "Hello There!\r\n" +
                                " \tFoo";

        SourceText st = SourceText.From(testText);

        var tl2 = st.TextLines[1];

        var col = tl2.GetColumnForOffset(tabSize: 4, charPositionInLine: 2);
        Assert.That(col, Is.EqualTo(4));
    }

    [Test]
    public void GetColumnForOffset3() {
        const string testText = "Hello There!\r\n" +
                                "  \tFoo";

        SourceText st = SourceText.From(testText);

        var tl2 = st.TextLines[1];

        var col = tl2.GetColumnForOffset(tabSize: 4, charPositionInLine: 3);
        Assert.That(col, Is.EqualTo(4));
    }

    [Test]
    public void GetColumnForOffset4() {
        const string testText = "Hello There!\r\n" +
                                "   \tFoo";

        SourceText st = SourceText.From(testText);

        var tl2 = st.TextLines[1];

        var col = tl2.GetColumnForOffset(tabSize: 4, charPositionInLine: 4);
        Assert.That(col, Is.EqualTo(4));
    }

    [Test]
    public void GetColumnForOffset5() {
        const string testText = "Hello There!\r\n" +
                                "    \tFoo";

        SourceText st = SourceText.From(testText);

        var tl2 = st.TextLines[1];

        var col = tl2.GetColumnForOffset(tabSize: 4, charPositionInLine: 5);
        Assert.That(col, Is.EqualTo(8));
    }

}