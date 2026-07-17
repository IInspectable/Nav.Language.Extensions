using System;
using System.Collections.Generic;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.Text;

// ReSharper disable GenericEnumeratorNotDisposed

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

    // ------------------------------------------------------------------------------------------------
    // Sicherheitsnetz: Grenzfälle von SourceText/GetTextLineAtPositionCore, GetLocation, Substring/Slice
    // und Zeilenzerlegung. Nagelt das heutige Verhalten fest, bevor an Validierung und Rändern geschraubt wird.
    // ------------------------------------------------------------------------------------------------

    [Test]
    public void EmptyIsSingleton() {
        Assert.That(SourceText.Empty, Is.SameAs(SourceText.Empty));
    }

    [Test]
    public void GetTextLineAtPosition_AtLength_NoTrailingNewline_ReturnsLastContentLine() {
        SourceText st = SourceText.From("first\r\nsecond");

        var line = st.GetTextLineAtPosition(st.Length);

        Assert.That(line.Line,       Is.EqualTo(1));
        Assert.That(line.ToString(), Is.EqualTo("second"));
    }

    [Test]
    public void GetTextLineAtPosition_AtLength_TrailingNewline_ReturnsEmptyLastLine() {
        SourceText st = SourceText.From("first\r\nsecond\r\n");

        var line = st.GetTextLineAtPosition(st.Length);

        Assert.That(line.Line,       Is.EqualTo(2));
        Assert.That(line.ToString(), Is.EqualTo(""));
    }

    [Test]
    public void GetTextLineAtPosition_Negative_Throws() {
        SourceText st = SourceText.From("abc");

        Assert.That(() => st.GetTextLineAtPosition(-1), Throws.InstanceOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void GetTextLineAtPosition_BeyondLength_Throws() {
        SourceText st = SourceText.From("abc");

        Assert.That(() => st.GetTextLineAtPosition(st.Length + 1), Throws.InstanceOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void GetTextLineAtPosition_HintCacheEquivalence_ForwardMatchesReverse() {
        // Gemischte Zeilenenden (\r\n und \n) sind hier der Prüfgegenstand → bewusst escaped statt Raw-String.
        const string testText = "line0\r\n" +
                                "line1\n"   +
                                "line2\r\n" +
                                "line3\n"   +
                                "line4\r\n" +
                                "line5\n"   +
                                "line6\r\n" +
                                "line7\n"   +
                                "line8\r\n" +
                                "line9\n"   +
                                "line10";

        var forward = SourceText.From(testText);
        var reverse = SourceText.From(testText);

        // Vorwärtslauf trifft den Hint-Pfad, Rückwärtslauf erzwingt die Binärsuche — beide müssen
        // für jede Position dieselbe (per Brute-Force ermittelte) Zeile liefern.
        for (int p = 0; p <= forward.Length; p++) {
            Assert.That(forward.GetTextLineAtPosition(p).Line, Is.EqualTo(ExpectedLineIndex(forward, p)), $"Vorwärts, Position {p}");
        }

        for (int p = reverse.Length; p >= 0; p--) {
            Assert.That(reverse.GetTextLineAtPosition(p).Line, Is.EqualTo(ExpectedLineIndex(reverse, p)), $"Rückwärts, Position {p}");
        }
    }

    [Test]
    public void GetTextLineAtPosition_HintWindowBoundaryAndBackwardJump() {
        const string testText = "line0\n" +
                                "line1\n" +
                                "line2\n" +
                                "line3\n" +
                                "line4\n" +
                                "line5\n" +
                                "line6\n" +
                                "line7\n" +
                                "line8\n" +
                                "line9\n" +
                                "line10";

        var st = SourceText.From(testText);

        // Hint auf Zeile 0 setzen.
        Assert.That(st.GetTextLineAtPosition(0).Line, Is.EqualTo(0));

        // Zeile lastLineNumber + 3 liegt knapp außerhalb des effektiven Hint-Fensters → Binärsuche.
        var posInLine3 = st.TextLines[3].Start + 1;
        Assert.That(st.GetTextLineAtPosition(posInLine3).Line, Is.EqualTo(3));

        // Vorwärtslauf setzt den Hint weit nach hinten …
        Assert.That(st.GetTextLineAtPosition(st.TextLines[9].Start).Line, Is.EqualTo(9));

        // … dann ein Sprung weit zurück (Position unterhalb TextLines[hint].Start → Binärsuche).
        var posInLine1 = st.TextLines[1].Start + 1;
        Assert.That(st.GetTextLineAtPosition(posInLine1).Line, Is.EqualTo(1));
    }

    [Test]
    public void GetLocation_MultiLineExtent() {
        const string testText = "Hello\r\nWorld\r\nEnd";

        var st = SourceText.From(testText);

        var start = testText.IndexOf("llo", StringComparison.Ordinal); //  2, Zeile 0
        var end   = testText.IndexOf("rld", StringComparison.Ordinal); //  9, Zeile 1
        var loc   = st.GetLocation(TextExtent.FromBounds(start, end));

        Assert.That(loc.StartLinePosition.Line, Is.EqualTo(0));
        Assert.That(loc.EndLinePosition.Line,   Is.EqualTo(1));
    }

    [Test]
    public void GetLocation_ExtentEndingAtLineEnd_MapsToNextLineChar0() {
        const string testText = "abc\r\ndef";

        var st = SourceText.From(testText);

        // [0,5] deckt "abc\r\n" ab; End == 5 == Start der Folgezeile → EndLinePosition = Zeile 1, Character 0.
        var loc = st.GetLocation(TextExtent.FromBounds(0, 5));

        Assert.That(loc.StartLinePosition.Line,      Is.EqualTo(0));
        Assert.That(loc.StartLinePosition.Character, Is.EqualTo(0));
        Assert.That(loc.EndLinePosition.Line,        Is.EqualTo(1));
        Assert.That(loc.EndLinePosition.Character,   Is.EqualTo(0));
    }

    [Test]
    public void GetLocation_EofPointExtent() {
        const string testText = "abc\r\ndef";

        var st = SourceText.From(testText);

        var loc = st.GetLocation(TextExtent.FromBounds(st.Length, st.Length));

        Assert.That(loc.StartLinePosition.Line,      Is.EqualTo(1));
        Assert.That(loc.StartLinePosition.Character, Is.EqualTo(3));
        Assert.That(loc.EndLinePosition.Line,        Is.EqualTo(1));
        Assert.That(loc.EndLinePosition.Character,   Is.EqualTo(3));
    }

    [Test]
    public void Substring_FullText() {
        const string testText = "Hello\r\nWorld";

        var st = SourceText.From(testText);

        Assert.That(st.Substring(TextExtent.FromBounds(0, st.Length)), Is.EqualTo(testText));
    }

    [Test]
    public void Substring_EmptyExtent() {
        var st = SourceText.From("Hello");

        Assert.That(st.Substring(TextExtent.FromBounds(2, 2)), Is.EqualTo(""));
    }

    [Test]
    public void Substring_AtEof() {
        var st = SourceText.From("Hello");

        Assert.That(st.Substring(TextExtent.FromBounds(st.Length, st.Length)), Is.EqualTo(""));
    }

    [Test]
    public void Substring_LengthBeyondEnd_Throws() {
        var st = SourceText.From("Hello");

        Assert.That(() => st.Substring(0, st.Length + 1), Throws.InstanceOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void Substring_NegativeStart_Throws() {
        var st = SourceText.From("Hello");

        Assert.That(() => st.Substring(-1, 2), Throws.InstanceOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void TextLines_LineFeedOnly() {
        const string testText = "a\nb\nc";

        var st = SourceText.From(testText);

        Assert.That(st.TextLines.Count,         Is.EqualTo(3));
        Assert.That(st.TextLines[0].ToString(), Is.EqualTo("a\n"));
        Assert.That(st.TextLines[1].ToString(), Is.EqualTo("b\n"));
        Assert.That(st.TextLines[2].ToString(), Is.EqualTo("c"));

        // Auf dem Trennzeichen selbst gehört die Position noch zur davorliegenden Zeile.
        var nlPos = testText.IndexOf('\n'); // 1
        Assert.That(st.GetTextLineAtPosition(nlPos).Line, Is.EqualTo(0));
    }

    [Test]
    public void TextLines_CarriageReturnOnly() {
        const string testText = "a\rb\rc";

        var st = SourceText.From(testText);

        Assert.That(st.TextLines.Count,         Is.EqualTo(3));
        Assert.That(st.TextLines[0].ToString(), Is.EqualTo("a\r"));
        Assert.That(st.TextLines[1].ToString(), Is.EqualTo("b\r"));
        Assert.That(st.TextLines[2].ToString(), Is.EqualTo("c"));

        var crPos = testText.IndexOf('\r'); // 1
        Assert.That(st.GetTextLineAtPosition(crPos).Line, Is.EqualTo(0));
    }

    [Test]
    public void GetLocation_MissingExtent_ThrowsWithExtentParamName() {
        var st = SourceText.From("Hello");

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => st.GetLocation(TextExtent.Missing));
        Assert.That(ex!.ParamName, Is.EqualTo("extent"));
    }

    [Test]
    public void GetLocation_ExtentEndBeyondLength_ThrowsWithExtentParamName() {
        var st = SourceText.From("Hello");

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => st.GetLocation(TextExtent.FromBounds(0, st.Length + 1)));
        Assert.That(ex!.ParamName, Is.EqualTo("extent"));
    }

    [Test]
    public void Enumerator_CurrentBeforeMoveNext_Throws() {
        var lines = SourceText.From("a\r\nb").TextLines;

        var e = lines.GetEnumerator();

        Assert.That(() => e.Current, Throws.InstanceOf<InvalidOperationException>());
    }

    [Test]
    public void Enumerator_CurrentAfterEnd_Throws() {
        var lines = SourceText.From("a\r\nb").TextLines;

        var e = lines.GetEnumerator();
        while (e.MoveNext()) {
        }

        Assert.That(() => e.Current, Throws.InstanceOf<InvalidOperationException>());
    }

    [Test]
    public void Enumerator_ForeachYieldsAllLines() {
        var lines = SourceText.From("a\r\nb\r\nc").TextLines;

        var collected = new List<int>();
        foreach (var line in lines) {
            collected.Add(line.Line);
        }

        Assert.That(collected, Is.EqualTo(new[] { 0, 1, 2 }));
    }

    [Test]
    public void Enumerator_ResetReturnsToBeforeStart() {
        var lines = SourceText.From("a\r\nb").TextLines;

        IEnumerator<SourceTextLine> e = lines.GetEnumerator();
        Assert.That(e.MoveNext(), Is.True);

        e.Reset();
        Assert.That(() => e.Current, Throws.InstanceOf<InvalidOperationException>());

        Assert.That(e.MoveNext(),   Is.True);
        Assert.That(e.Current.Line, Is.EqualTo(0));
    }

    /// <summary>
    /// Ermittelt die Zeile, die <paramref name="position"/> enthält, ohne den Hint-Cache von
    /// <c>GetTextLineAtPositionCore</c> zu berühren — dient als unabhängige Erwartung im Äquivalenztest.
    /// </summary>
    static int ExpectedLineIndex(SourceText st, int position) {
        if (position == st.Length) {
            return st.TextLines.Count - 1;
        }

        for (int i = 0; i < st.TextLines.Count; i++) {
            var line = st.TextLines[i];
            if (position >= line.Start && position < line.End) {
                return i;
            }
        }

        throw new InvalidOperationException($"Keine Zeile für Position {position} gefunden.");
    }

}
