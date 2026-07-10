#region Using Directives

using NUnit.Framework;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.Formatting;
using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Nav.Language.Tests.Formatting;

/// <summary>
/// Golden-Tests der S3-Spaltenausrichtung: Pfeil-Spalte (inkl. Exit-Transitionen mit tightem
/// <c>:Port</c> in der kanonischen Breite), das 3-Spalten-Node-Raster <c>keyword | node | rest</c>,
/// die Gruppenbildung (<c>interruptLines ≥ 2</c>, Größe-1-Ausnahme, Ausschlüsse), der Task-Kopf
/// (Blöcke stapeln, Pull-up des ersten Blocks, mehrzeiliges <c>[params]</c> unter dem ersten
/// Parameter) und der einzeilig normalisierte <c>taskref</c>-Kopf — jeweils mit Idempotenz-Prüfung.
/// Default-Policy ist <c>NextTabStop</c> (per Korpus-Kalibrierung bestätigt); Padding ist immer Spaces.
/// </summary>
[TestFixture]
public class NavFormattingAlignmentGoldenTests {

    static readonly TextEditorSettings Settings = new(tabSize: 4, newLine: "\r\n");

    static readonly NavFormattingOptions SpacesOptions = NavFormattingOptions.Default with { IndentStyle = IndentStyle.Spaces };

    static string Format(string text, NavFormattingOptions options = null) {
        var changes = NavFormattingService.FormatDocument(SyntaxTree.ParseText(text), Settings, options ?? SpacesOptions);
        return new TextChangeWriter().ApplyTextChanges(text, changes);
    }

    static void AssertFormat(string source, string expected, NavFormattingOptions options = null) {
        Assert.That(Format(source, options), Is.EqualTo(expected));
        Assert.That(Format(expected, options), Is.EqualTo(expected), "Das Golden selbst muss ein Fixpunkt sein (Idempotenz).");
    }

    // ---- Pfeil-Spalte ---------------------------------------------------------------------------

    [Test]
    public void ArrowsAreAlignedToTheNextTabStop() {
        // Kanonische Breiten: init = 4, Choice = 6, Dialog:Ok = 9 (tight ':') -> tightMin 10 -> Spalte 12.
        var source = """
        task Sample
        {
            init          -->Choice;
            Choice     o-> Dialog;
            Dialog:Ok-->Exit;
        }
        """;
        var expected = """
        task Sample
        {
            init        --> Choice;
            Choice      o-> Dialog;
            Dialog:Ok   --> Exit;
        }

        """;

        AssertFormat(source, expected);
    }

    [Test]
    public void SingleTransitionIsNotAligned() {
        // Gruppen der Größe 1 bekommen kein Tab-Stopp-Padding — Single-Space-Idiom.
        var source = """
        task Sample
        {
            I1   -->   E;
        }
        """;
        var expected = """
        task Sample
        {
            I1 --> E;
        }

        """;

        AssertFormat(source, expected);
    }

    [Test]
    public void SingleBlankLineOrCommentLineDoesNotBreakTheGroup() {
        var source = """
        task Sample
        {
            I1 --> E;

            LongSource --> E;
            // Kommentar
            X1 --> E;
        }
        """;
        var expected = """
        task Sample
        {
            I1          --> E;

            LongSource  --> E;
            // Kommentar
            X1          --> E;
        }

        """;

        AssertFormat(source, expected);
    }

    [Test]
    public void TwoInterruptLinesStartANewGroup() {
        // Zwei Leerzeilen trennen; jede Gruppe rechnet ihre eigene Spalte (12 bzw. 4).
        var source = """
        task Sample
        {
            LongSource --> E;
            I1 --> E;


            X --> E;
            Y --> E;
        }
        """;
        var expected = """
        task Sample
        {
            LongSource  --> E;
            I1          --> E;


            X   --> E;
            Y   --> E;
        }

        """;

        AssertFormat(source, expected);
    }

    [Test]
    public void InlineBlockCommentBeforeArrowExcludesFromColumnButKeepsGroup() {
        // Die Spaltenbreite darf nie an einer Kommentar-Textlänge hängen: die kommentierte Transition
        // wird nur normalisiert, die Nachbarn bleiben eine Gruppe (Spalte über I1/B:Out -> 8).
        var source = """
        task Sample
        {
            I1 --> E;
            A/* x */--> E;
            B:Out --> E;
        }
        """;
        var expected = """
        task Sample
        {
            I1      --> E;
            A /* x */ --> E;
            B:Out   --> E;
        }

        """;

        AssertFormat(source, expected);
    }

    [Test]
    public void MultiLineTransitionBreaksTheGroupAndIsExcluded() {
        // Eine hand-gelegte Anweisung bricht die Gruppe: die Nachbarn sind danach Größe-1-Gruppen.
        var source = """
        task Sample
        {
            I1 --> E;
            A
            --> E;
            B --> E;
        }

        """;

        AssertFormat(source, source);
    }

    // ---- Node-Grid (keyword | node | rest) ------------------------------------------------------

    [Test]
    public void NodeGridAlignsNodeAndRestColumns() {
        // Spalte node hinter dem längsten Keyword (choice, 6 -> 8); Spalte rest hinter dem längsten
        // node (LongerTypeName endet auf 22 -> 24). choice hat keine Spalte 3 -> kein Phantom-Padding.
        var source = """
        task Sample
        {
            task Foo Alias1;
            init Start [params int x];
            choice Decide;
            task LongerTypeName Alias2;
        }
        """;
        var expected = """
        task Sample
        {
            task    Foo             Alias1;
            init    Start           [params int x];
            choice  Decide;
            task    LongerTypeName  Alias2;
        }

        """;

        AssertFormat(source, expected);
    }

    [Test]
    public void EndNodeHasNoNodeColumnAndGetsNoPhantomPadding() {
        var source = """
        task Sample
        {
            init I1;
            view V;
            end;
        }
        """;
        var expected = """
        task Sample
        {
            init    I1;
            view    V;
            end;
        }

        """;

        AssertFormat(source, expected);
    }

    [Test]
    public void RestColumnNeedsAtLeastTwoParticipants() {
        // Nur eine Zeile hat einen Rest (der Alias) -> Spalte 3 entfällt, Single-Space.
        var source = """
        task Sample
        {
            init I1;
            task Worker w;
        }
        """;
        var expected = """
        task Sample
        {
            init    I1;
            task    Worker w;
        }

        """;

        AssertFormat(source, expected);
    }

    // ---- Task-Kopf ------------------------------------------------------------------------------

    [Test]
    public void TaskHeadBlocksAreStacked() {
        // Block 1 ein Space hinter dem Identifier, weitere Blöcke linksbündig darunter (Spalte 12).
        var source = """
        task Sample [code Foo] [params int x, string label] [result bool]
        {
        }
        """;
        var expected = """
        task Sample [code Foo]
                    [params int x, string label]
                    [result bool]
        {
        }

        """;

        AssertFormat(source, expected);
    }

    [Test]
    public void BrokenFirstHeadBlockIsPulledUpBehindTheIdentifier() {
        // Kanonisierung: der erste Block wird hochgezogen — bloße authored Newlines sind keine
        // Renderer-Schranke (Pull-up-Ausnahme).
        var source = """
        task Sample
            [code Foo]
        {
        }
        """;
        var expected = """
        task Sample [code Foo]
        {
        }

        """;

        AssertFormat(source, expected);
    }

    [Test]
    public void CommentForcesFirstHeadBlockOntoTheHeadColumn() {
        // Ein '//'-Kommentar in der Lücke Id -> Block 1 verhindert das Hochziehen (harte Schranke);
        // Block 1 fällt dann wie ein Folgeblock auf die kanonische Kopf-Spalte.
        var source = """
        task Sample // Kommentar
        [code Foo]
        {
        }
        """;
        var expected = """
        task Sample // Kommentar
                    [code Foo]
        {
        }

        """;

        AssertFormat(source, expected);
    }

    [Test]
    public void AuthoredMultiLineParamsAreAlignedUnderTheFirstParameter() {
        // Autor-Umbruch bewahrt: Folgeparameter unter dem ersten (Spalte 11 + "[params " = 19),
        // ']' tight am letzten Parameter; der umbrochene erste Block wird hochgezogen.
        var source = """
        task Other
          [params int x,
          string label]
        {
        }
        """;
        var expected = """
        task Other [params int x,
                           string label]
        {
        }

        """;

        AssertFormat(source, expected);
    }

    [Test]
    public void EmptyParamsBlockStaysTight() {
        // Grenzfall aus dem Korpus-Smoke: ein leeres [params] hat keinen ersten Parameter — die Lücke
        // params -> ']' gehört der PunctuationRule (tight), nicht dem Task-Kopf-Layout.
        var source = """
        task Sample [params]
        {
        }
        """;
        var expected = """
        task Sample [params]
        {
        }

        """;

        AssertFormat(source, expected);
    }

    [Test]
    public void SingleLineParamsStaySingleLine() {
        var source = """
        task Other [params int x,string label]
        {
        }
        """;
        var expected = """
        task Other [params int x, string label]
        {
        }

        """;

        AssertFormat(source, expected);
    }

    // ---- taskref-Kopf ---------------------------------------------------------------------------

    [Test]
    public void TaskrefHeadIsNormalizedToASingleLine() {
        // Kein Stapeln im taskref-Kopf: die leichten Blöcke werden (auch über authored Umbrüche
        // hinweg) einzeilig gezogen; die Connection-Points nehmen am Node-Grid teil.
        var source = """
        taskref Legacy
            [namespaceprefix Foo.Bar]
            [result bool r]
        {
            init I;
            exit O;
        }
        """;
        var expected = """
        taskref Legacy [namespaceprefix Foo.Bar] [result bool r]
        {
            init    I;
            exit    O;
        }

        """;

        AssertFormat(source, expected);
    }

    // ---- Policy & Optionen ----------------------------------------------------------------------

    [Test]
    public void TightPolicyUsesExactlyOneSpaceBehindTheWidestRow() {
        var options = SpacesOptions with { AlignmentColumnPolicy = AlignmentColumnPolicy.Tight };
        var source = """
        task Sample
        {
            I1 --> E;
            LongSource --> E;
        }
        """;
        var expected = """
        task Sample
        {
            I1         --> E;
            LongSource --> E;
        }

        """;

        AssertFormat(source, expected, options);
    }

    [Test]
    public void AlignmentCanBeTurnedOff() {
        // Alle Ausrichtungen aus -> überall das Single-Space-Idiom, Kopf-Blöcke bleiben inline.
        var options = SpacesOptions with { AlignArrows = false, AlignNodeGrid = false, AlignTaskHeadBlocks = false };
        var source = """
        task Sample [code Foo] [result bool]
        {
            init Start;
            choice Decide;

            Start --> Decide;
            LongSource --> Decide;
        }

        """;

        AssertFormat(source, source, options);
    }

}
