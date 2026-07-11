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
/// Parameter) und der symmetrisch stapelnde <c>taskref</c>-Kopf — jeweils mit Idempotenz-Prüfung.
/// Default-Policy ist <c>NextTabStop</c> (per Korpus-Kalibrierung bestätigt); Padding ist immer Spaces.
/// </summary>
[TestFixture]
public class NavFormattingAlignmentGoldenTests {

    static readonly TextEditorSettings Settings = new(tabSize: 4, newLine: "\r\n");

    static readonly NavFormattingOptions SpacesOptions = NavFormattingOptions.Default with { IndentStyle = IndentStyle.Spaces };

    static string Format(string text, NavFormattingOptions options = null) {
        var changes = NavFormattingService.FormatDocument(SyntaxTree.ParseText(text), Settings, (options ?? SpacesOptions) with { VerifyResult = true });
        return new TextChangeWriter().ApplyTextChanges(text, changes);
    }

    static void AssertFormat(string source, string expected, NavFormattingOptions options = null) {
        Assert.That(Format(source,   options), Is.EqualTo(expected));
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

    // ---- Condition-Spalte (if / else if / else) -------------------------------------------------

    [Test]
    public void ConditionsAreAlignedToTheNextTabStop() {
        // Die erste, unbedingte Kante bricht die Gruppe nicht (nur kein Teilnehmer). Condition-Spalte
        // tight hinter dem längsten Ziel-Teil: "Src --> AktionErweiterteSuche" = 29 -> Spalte 30, die
        // breiteste Zeile bekommt also genau einen Space. Beim else if wird nur das führende 'else'
        // ausgerichtet, das innere 'if' bleibt Single-Space.
        var source = """
                     task Sample
                     {
                         Src --> First;
                         Src --> AktionSuchen if "a";
                         Src --> AktionErweiterteSuche else if "b";
                         Src --> ArtikelBearbeiten else;
                     }
                     """;
        var expected = """
                       task Sample
                       {
                           Src --> First;
                           Src --> AktionSuchen          if "a";
                           Src --> AktionErweiterteSuche else if "b";
                           Src --> ArtikelBearbeiten     else;
                       }

                       """;

        AssertFormat(source, expected);
    }

    [Test]
    public void SingleConditionIsNotAligned() {
        // Nur eine Transition der Gruppe trägt eine Bedingung -> kein Tab-Stopp-Padding, Single-Space.
        var source = """
                     task Sample
                     {
                         Src --> LongTarget if "a";
                         Src --> B;
                     }
                     """;
        var expected = """
                       task Sample
                       {
                           Src --> LongTarget if "a";
                           Src --> B;
                       }

                       """;

        AssertFormat(source, expected);
    }

    [Test]
    public void ConditionsAlignEvenWhenArrowsAreNotAligned() {
        // Unabhängige Optionen: Pfeile bleiben Single-Space, die Bedingungen richten sich trotzdem aus
        // (tight hinter "Src --> AktionSuchen" = 20 -> Spalte 21).
        var options = SpacesOptions with { AlignArrows = false };
        var source = """
                     task Sample
                     {
                         Src --> AktionSuchen if "a";
                         LongSrc --> B else;
                     }
                     """;
        var expected = """
                       task Sample
                       {
                           Src --> AktionSuchen if "a";
                           LongSrc --> B        else;
                       }

                       """;

        AssertFormat(source, expected, options);
    }

    [Test]
    public void ExitTransitionConditionsAreAligned() {
        // Exit-Transitionen tragen den tighten ':ExitPort' im Quell-Teil (in der kanonischen Breite
        // enthalten). Nach der (ebenfalls ausgerichteten) Pfeil-Spalte rasten die Bedingungen tight
        // hinter dem längsten Ziel-Teil ein.
        var source = """
                     task Sample
                     {
                         A:Done --> Foo if "a";
                         Longer:Cancel --> B else if "b";
                     }
                     """;
        var expected = """
                       task Sample
                       {
                           A:Done          --> Foo if "a";
                           Longer:Cancel   --> B   else if "b";
                       }

                       """;

        AssertFormat(source, expected);
    }

    [Test]
    public void SingleBlankLineBreaksTheConditionGroup() {
        // Wie bei den Trailing-Kommentaren bricht bereits eine einzelne Leerzeile den Condition-Block —
        // die beiden Bedingungen sind danach je Größe-1-Gruppen und bleiben Single-Space (kein
        // gemeinsames 'if'). Die Pfeile bleiben eine Gruppe (dort bricht erst die zweite Leerzeile).
        var source = """
                     task Sample
                     {
                         Src --> AktionSuchen if "a";

                         Src --> B else;
                     }
                     """;
        var expected = """
                       task Sample
                       {
                           Src --> AktionSuchen if "a";

                           Src --> B else;
                       }

                       """;

        AssertFormat(source, expected);
    }

    [Test]
    public void ConditionAlignmentCanBeTurnedOff() {
        var options = SpacesOptions with { AlignConditions = false };
        var source = """
                     task Sample
                     {
                         Src --> AktionSuchen if "a";
                         Src --> B else;
                     }
                     """;
        var expected = """
                       task Sample
                       {
                           Src --> AktionSuchen if "a";
                           Src --> B else;
                       }

                       """;

        AssertFormat(source, expected, options);
    }

    // ---- Trigger-Spalte (on … / spontaneous) ----------------------------------------------------

    [Test]
    public void TriggersAlignTightBehindTheLongestTarget() {
        // Trigger-Spalte tight hinter dem längsten Ziel-Teil ("Src --> LongTarget" = 18 -> Spalte 19);
        // die breiteste Zeile bekommt genau einen Space. Pfeile richten sich unabhängig davon aus.
        var source = """
                     task Sample
                     {
                         Src --> A on Trigger1;
                         Src --> LongTarget on Trigger2;
                     }
                     """;
        var expected = """
                       task Sample
                       {
                           Src --> A          on Trigger1;
                           Src --> LongTarget on Trigger2;
                       }

                       """;

        AssertFormat(source, expected);
    }

    [Test]
    public void SpontaneousTriggerParticipatesInTheColumn() {
        // Auch der schlüsselwort-basierte 'spontaneous'-Trigger richtet sich an der Trigger-Spalte aus.
        var source = """
                     task Sample
                     {
                         Src --> A spontaneous;
                         Src --> LongTarget on T2;
                     }
                     """;
        var expected = """
                       task Sample
                       {
                           Src --> A          spontaneous;
                           Src --> LongTarget on T2;
                       }

                       """;

        AssertFormat(source, expected);
    }

    [Test]
    public void SingleTriggerIsNotAligned() {
        // Nur eine Transition der Gruppe trägt einen Trigger -> kein Padding, Single-Space.
        var source = """
                     task Sample
                     {
                         Src --> LongTarget on T1;
                         Src --> B;
                     }
                     """;
        var expected = """
                       task Sample
                       {
                           Src --> LongTarget on T1;
                           Src --> B;
                       }

                       """;

        AssertFormat(source, expected);
    }

    [Test]
    public void SingleBlankLineBreaksTheTriggerGroup() {
        // Wie bei den Trailing-Kommentaren bricht bereits eine einzelne Leerzeile den Trigger-Block:
        // jede Gruppe rechnet ihre eigene Spalte (19 bzw. 11). Die Pfeile bleiben eine Gruppe (dort
        // bricht erst die zweite Leerzeile) — hier trivial, weil alle Quell-Teile "Src" sind.
        var source = """
                     task Sample
                     {
                         Src --> A on T1;
                         Src --> LongTarget on T2;

                         Src --> Bb on T3;
                         Src --> C on T4;
                     }
                     """;
        var expected = """
                       task Sample
                       {
                           Src --> A          on T1;
                           Src --> LongTarget on T2;

                           Src --> Bb on T3;
                           Src --> C  on T4;
                       }

                       """;

        AssertFormat(source, expected);
    }

    [Test]
    public void TriggersAndConditionsBuildOnEachOther() {
        // Die Spalten stapeln in Quellreihenfolge: die Trigger-Spalte baut auf die Pfeil-Spalte auf, die
        // Condition-Spalte auf beide. 'on' fluchtet auf Spalte 19, 'if' auf Spalte 28.
        var source = """
                     task Sample
                     {
                         Src --> A on T1 if "a";
                         Src --> LongTarget on Trig2 if "b";
                     }
                     """;
        var expected = """
                       task Sample
                       {
                           Src --> A          on T1    if "a";
                           Src --> LongTarget on Trig2 if "b";
                       }

                       """;

        AssertFormat(source, expected);
    }

    [Test]
    public void TriggerAlignmentCanBeTurnedOff() {
        var options = SpacesOptions with { AlignTriggers = false };
        var source = """
                     task Sample
                     {
                         Src --> A on T1;
                         Src --> LongTarget on T2;
                     }
                     """;
        var expected = """
                       task Sample
                       {
                           Src --> A on T1;
                           Src --> LongTarget on T2;
                       }

                       """;

        AssertFormat(source, expected, options);
    }

    // ---- Node-Grid (keyword | node | rest) ------------------------------------------------------

    [Test]
    public void NodeGridAlignsNodeAndRestColumns() {
        // Spalte node hinter dem längsten Keyword (choice, 6 -> 8); Spalte rest (der Alias) hinter dem
        // längsten node (LongerTypeName endet auf 22 -> 24). choice hat keine Spalte 3 -> kein
        // Phantom-Padding. Der [params]-Block nimmt NICHT an der Rest-Spalte teil, sondern steht nur mit
        // einem Space hinter dem node.
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
                           init    Start [params int x];
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

    [Test]
    public void NodeParamsBlocksAlignAmongThemselves() {
        // Aufeinanderfolgende [params]-Blöcke werden untereinander ausgerichtet — tight, ein Space hinter
        // dem längsten node (KalkulationFromArtikelsuche endet auf Spalte 35 -> [params ab Spalte 36).
        var source = """
                     task Sample
                     {
                         init Kalkulation [params BORef a, bool b];
                         init KalkulationFromArtikelsuche [params BORef a];
                     }
                     """;
        var expected = """
                       task Sample
                       {
                           init    Kalkulation                 [params BORef a, bool b];
                           init    KalkulationFromArtikelsuche [params BORef a];
                       }

                       """;

        AssertFormat(source, expected);
    }

    [Test]
    public void SingleNodeParamsStaysTightAndDoesNotWander() {
        // Nur ein [params] in der Gruppe -> keine Ausrichtung, nur ein Space (kein Wandern nach rechts),
        // obwohl die node-Spalte (2 Teilnehmer) ausgerichtet wird.
        var source = """
                     task Sample
                     {
                         init OnlyOne [params int x];
                         init Other;
                     }
                     """;
        var expected = """
                       task Sample
                       {
                           init    OnlyOne [params int x];
                           init    Other;
                       }

                       """;

        AssertFormat(source, expected);
    }

    [Test]
    public void NodeParamsColumnIsSeparateFromAliasColumn() {
        // Der [params]-Block wird NICHT mit einem langen Alias in dieselbe Spalte gezwängt: der Alias
        // richtet sich in der Rest-Spalte aus, die beiden [params] tight unter sich.
        var source = """
                     task Sample
                     {
                         task VeryLongTaskType SomeAlias;
                         init A [params int x];
                         choice Bbb [params int y];
                     }
                     """;
        var expected = """
                       task Sample
                       {
                           task    VeryLongTaskType SomeAlias;
                           init    A   [params int x];
                           choice  Bbb [params int y];
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
    public void BlankLinesBetweenStackedHeadBlocksAreCollapsed() {
        // Der Task-Kopf ist ein kanonisch erzwungener Stapel — wie Block 1 per Pull-up seine authored
        // Newlines verliert, kollabieren die gestapelten Folgeblöcke authored Leerzeilen dazwischen.
        var source = """
                     task BatchDisponieren        [base StandardWFS<TaskState> : IWFServiceBase]

                                                 [result Ignore]
                     {
                     }
                     """;
        var expected = """
                       task BatchDisponieren [base StandardWFS<TaskState>: IWFServiceBase]
                                             [result Ignore]
                       {
                       }

                       """;

        AssertFormat(source, expected);
    }

    [Test]
    public void CommentBetweenStackedHeadBlocksSurvivesWhileBlankLinesAreCollapsed() {
        // Ein Kommentar zwischen den Blöcken bleibt (eigene Zeile auf der Kopf-Spalte) — nur die Leerzeilen
        // um ihn herum entfallen (CapBlankRuns zählt nur leere Zeilen, Kommentarzeilen setzen den Lauf zurück).
        var source = """
                     task Sample [code Foo]

                                 // Hinweis

                                 [result bool]
                     {
                     }
                     """;
        var expected = """
                       task Sample [code Foo]
                                   // Hinweis
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
    public void BlankLinesBetweenMultiLineParamsAreCollapsed() {
        // Auch das mehrzeilige [params] ist ein kanonischer Stapel (NewLineAlignedColumn(ParamsList)) —
        // eine Leerzeile zwischen zwei Parametern kollabiert wie zwischen Kopf-Blöcken.
        var source = """
                     task Other [params int x,

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
    public void TaskrefHeadStacksBlocksLikeTaskHead() {
        // Symmetrie zum Task-Kopf: Block 1 inline hinter dem Identifier (Pull-up), jeder Folgeblock
        // gestapelt unter dem '[' des ersten; die Connection-Points nehmen am Node-Grid teil.
        var source = """
                     taskref Legacy [namespaceprefix Foo.Bar] [result bool r]
                     {
                         init I;
                         exit O;
                     }
                     """;
        var expected = """
                       taskref Legacy [namespaceprefix Foo.Bar]
                                      [result bool r]
                       {
                           init    I;
                           exit    O;
                       }

                       """;

        AssertFormat(source, expected);
    }

    [Test]
    public void TaskrefHeadCollapsesBlankLinesBetweenStackedBlocks() {
        // Symmetrie zum Task-Kopf: der Leerzeilen-Kollaps des gestapelten Kopfs gilt auch für taskref
        // (dieselbe TaskHeadLayoutRule -> NewLineAlignedColumn(TaskHeadBlock)).
        var source = """
                     taskref Legacy [namespaceprefix Foo.Bar]

                                    [result bool r]
                     {
                         init I;
                         exit O;
                     }
                     """;
        var expected = """
                       taskref Legacy [namespaceprefix Foo.Bar]
                                      [result bool r]
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
