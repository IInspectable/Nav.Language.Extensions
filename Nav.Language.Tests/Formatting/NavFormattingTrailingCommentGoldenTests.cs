#region Using Directives

using NUnit.Framework;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.Formatting;
using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Nav.Language.Tests.Formatting;

/// <summary>
/// Golden-Tests der Trailing-<c>//</c>-Kommentar-Ausrichtung: die Zeilenend-Kommentare eines
/// zusammenhängenden Anweisungs-Blocks werden an einer gemeinsamen Spalte ausgerichtet — <b>tight</b>
/// (genau ein Space hinter der längsten Zeile der Gruppe). Anders als die übrigen Ausrichtungen bricht
/// hier bereits <b>eine einzelne</b> Leerzeile bzw. Kommentarzeile den Block; eine kommentarlose Zeile
/// bricht ihn dagegen nicht (sie nimmt nur nicht teil). Node-Deklarationen und Transitionen bilden — wie
/// bei den übrigen Spalten — getrennte Gruppen. Jeweils mit Idempotenz-Prüfung.
/// </summary>
[TestFixture]
public class NavFormattingTrailingCommentGoldenTests {

    static readonly TextEditorSettings Settings = new(tabSize: 4, newLine: "\r\n");

    static readonly NavFormattingOptions SpacesOptions = NavFormattingOptions.Default with { IndentStyle = IndentStyle.Spaces };

    static string Format(string text, NavFormattingOptions options = null) {
        var changes = NavFormattingService.FormatDocument(SyntaxTree.ParseText(text), Settings, (options ?? SpacesOptions) with { VerifyResult = true });
        return new TextChangeWriter().ApplyTextChanges(text, changes);
    }

    static void AssertFormat(string source, string expected, NavFormattingOptions options = null) {
        Assert.That(Format(source, options), Is.EqualTo(expected));
        Assert.That(Format(expected, options), Is.EqualTo(expected), "Das Golden selbst muss ein Fixpunkt sein (Idempotenz).");
    }

    [Test]
    public void TrailingCommentsAlignAcrossABlock() {
        // Kommentar-Spalte tight hinter der längsten Zeile ("B --> Exit;" = 17 -> Spalte 18); die
        // breiteste Zeile bekommt genau einen Space. Pfeile richten sich unabhängig davon aus.
        var source = """
        task Sample
        {
            init --> A; // erste
            A --> B; // zweite
            B --> Exit; // dritte
        }
        """;
        var expected = """
        task Sample
        {
            init    --> A;    // erste
            A       --> B;    // zweite
            B       --> Exit; // dritte
        }

        """;

        AssertFormat(source, expected);
    }

    [Test]
    public void SingleBlankLineBreaksTheBlock() {
        // Bereits eine einzelne Leerzeile trennt (Schwelle 1) -> zwei Gruppen der Größe 1 -> keine
        // Kommentar-Ausrichtung, nur je ein Space.
        var source = """
        task Sample
        {
            init --> A; // erste

            B --> Exit; // dritte
        }
        """;
        var expected = """
        task Sample
        {
            init    --> A; // erste

            B       --> Exit; // dritte
        }

        """;

        AssertFormat(source, expected);
    }

    [Test]
    public void CommentLessLineDoesNotBreakTheBlock() {
        // Eine kommentarlose Zeile ist kein Teilnehmer, bricht die Gruppe aber nicht: die beiden
        // kommentierten Zeilen richten sich weiterhin aus.
        var source = """
        task Sample
        {
            init --> A; // erste
            A --> B;
            B --> Exit; // dritte
        }
        """;
        var expected = """
        task Sample
        {
            init    --> A;    // erste
            A       --> B;
            B       --> Exit; // dritte
        }

        """;

        AssertFormat(source, expected);
    }

    [Test]
    public void SingleTrailingCommentIsNotAligned() {
        // Nur eine Zeile der Gruppe trägt einen Trailing-Kommentar -> keine Ausrichtung, ein Space.
        var source = """
        task Sample
        {
            init --> A; // only
            A --> B;
        }
        """;
        var expected = """
        task Sample
        {
            init    --> A; // only
            A       --> B;
        }

        """;

        AssertFormat(source, expected);
    }

    [Test]
    public void NodeGridTrailingCommentsAlign() {
        // Auch die Node-Grid-Zeilen richten ihre Trailing-Kommentare aus ("task Worker w;" = 17 ->
        // Spalte 18), nachdem das keyword|node|rest-Raster gesetzt ist.
        var source = """
        task Sample
        {
            init Start; // i
            choice Decide; // c
            task Worker w; // w
        }
        """;
        var expected = """
        task Sample
        {
            init    Start;    // i
            choice  Decide;   // c
            task    Worker w; // w
        }

        """;

        AssertFormat(source, expected);
    }

    [Test]
    public void InlineBlockCommentLineIsExcludedButKeepsTheBlock() {
        // Die Zeile mit Inline-Block-Kommentar wird aus der Kommentar-Spalte ausgeschlossen (die
        // Spaltenbreite darf nie an einer Kommentar-Textlänge hängen), bricht die Gruppe aber nicht.
        var source = """
        task Sample
        {
            init --> A; // a
            B /* x */ --> C; // b
            D --> E; // c
        }
        """;
        var expected = """
        task Sample
        {
            init    --> A; // a
            B /* x */ --> C; // b
            D       --> E; // c
        }

        """;

        AssertFormat(source, expected);
    }

    [Test]
    public void OwnLineCommentBreaksTheBlock() {
        // Eine eigene Kommentarzeile zählt wie eine Leerzeile (interruptLines 1) und bricht den Block.
        var source = """
        task Sample
        {
            init --> A; // erste
            // section
            B --> Exit; // dritte
        }
        """;
        var expected = """
        task Sample
        {
            init    --> A; // erste
            // section
            B       --> Exit; // dritte
        }

        """;

        AssertFormat(source, expected);
    }

    [Test]
    public void NodeAndTransitionCommentsAreSeparateBlocks() {
        // Node-Deklarationen und Transitionen werden getrennt gruppiert (die grammatikalisch erzwungene
        // Leerzeile dazwischen trennt sie ohnehin) -> je eine Gruppe der Größe 1, keine Ausrichtung.
        var source = """
        task Sample
        {
            init Start; // node
            Start --> Exit; // transition
        }
        """;
        var expected = """
        task Sample
        {
            init Start; // node

            Start --> Exit; // transition
        }

        """;

        AssertFormat(source, expected);
    }

    [Test]
    public void TaskrefConnectionPointCommentsAlign() {
        var source = """
        taskref Legacy
        {
            init I; // in
            exit LongerOut; // out
        }
        """;
        var expected = """
        taskref Legacy
        {
            init    I;         // in
            exit    LongerOut; // out
        }

        """;

        AssertFormat(source, expected);
    }

    [Test]
    public void TrailingCommentAlignmentCanBeTurnedOff() {
        var options = SpacesOptions with { AlignTrailingComments = false };
        var source = """
        task Sample
        {
            init --> A; // erste
            A --> B; // zweite
        }
        """;
        var expected = """
        task Sample
        {
            init    --> A; // erste
            A       --> B; // zweite
        }

        """;

        AssertFormat(source, expected, options);
    }

}
