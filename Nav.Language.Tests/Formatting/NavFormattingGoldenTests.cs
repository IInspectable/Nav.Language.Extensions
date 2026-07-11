#region Using Directives

using NUnit.Framework;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.Formatting;
using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Nav.Language.Tests.Formatting;

/// <summary>
/// Golden-Tests des Layout-Regelsatzes fehlerfreier Dateien: Allman-Klammern, Member-/Statement-
/// Umbrüche, die Leerzeile vor den Transitionen, tight <c>Node:Port</c>, Interpunktion und Typ-Interna,
/// Kommentar-Normalisierung, Direktiven ab Spalte 0, Datei-Anfang (Fehl-Einzug/Kopf-Kommentare) sowie
/// Final-Newline und EOF-Trailing-Trim. Die Erwartungen enthalten die Spaltenausrichtung (Pfeile,
/// Node-Grid) mit; deren eigene Goldens liegen in <see cref="NavFormattingAlignmentGoldenTests"/>.
/// </summary>
[TestFixture]
public class NavFormattingGoldenTests {

    static readonly TextEditorSettings Settings = new(tabSize: 4, newLine: "\r\n");

    // Goldens mit Spaces-Einzug, damit die Raw-String-Erwartungen lesbar bleiben; der Tab-Default wird
    // in TabsAreTheDefaultIndentStyle einmal explizit (escaped) festgenagelt.
    static readonly NavFormattingOptions SpacesOptions = NavFormattingOptions.Default with { IndentStyle = IndentStyle.Spaces };

    static string Format(string text, NavFormattingOptions options = null) {
        var changes = NavFormattingService.FormatDocument(SyntaxTree.ParseText(text), Settings, (options ?? SpacesOptions) with { VerifyResult = true });
        return new TextChangeWriter().ApplyTextChanges(text, changes);
    }

    static void AssertFormat(string source, string expected, NavFormattingOptions options = null) {
        Assert.That(Format(source,   options), Is.EqualTo(expected));
        Assert.That(Format(expected, options), Is.EqualTo(expected), "Das Golden selbst muss ein Fixpunkt sein (Idempotenz).");
    }

    // ---- Struktur: Allman, Member-/Statement-Breaks, Leerzeile vor Transitionen -----------------

    [Test]
    public void CanonicalFileIsAFixpoint() {
        var canonical = """
                        [namespaceprefix Sample.Namespace]

                        [using System]

                        taskref "Other.nav";

                        task Sample [params string label]
                        {
                            init    I1;
                            task    Worker w;
                            exit    E;

                            I1      --> E;
                            w:Out   --> E;
                        }

                        """;

        Assert.That(NavFormattingService.FormatDocument(SyntaxTree.ParseText(canonical), Settings, SpacesOptions),
                    Is.Empty, "Eine bereits kanonische Datei liefert 0 Changes.");
    }

    [Test]
    public void SingleLineTaskIsBrokenIntoAllmanLayout() {
        var source = """
                     task Sample{init I1;exit E;I1-->E;}
                     """;
        var expected = """
                       task Sample
                       {
                           init    I1;
                           exit    E;

                           I1 --> E;
                       }

                       """;

        AssertFormat(source, expected);
    }

    [Test]
    public void BlankLineBeforeTransitionsIsToppedUpButNeverCollapsed() {
        // Drei Autoren-Leerzeilen bleiben erhalten (Minimum 1 kappt nie nach oben).
        var source = """
                     task Sample
                     {
                         init    I1;
                         exit    E;



                         I1 --> E;
                     }

                     """;

        AssertFormat(source, source);
    }

    [Test]
    public void MemberBreaksPutEveryTopLevelMemberOnItsOwnLine() {
        // Jeder Member auf eigener Zeile; zusätzlich hebt BlankLineAroundBlockMembersRule das Leerzeilen-
        // Minimum an: eine Leerzeile nach [namespaceprefix] und vor dem task-Block — die [using] unter sich
        // bleiben tight (flach, kein erzwungenes Minimum).
        var source = """
                     [namespaceprefix N] [using A] [using B] task X
                     {
                     }
                     """;
        var expected = """
                       [namespaceprefix N]

                       [using A]
                       [using B]

                       task X
                       {
                       }

                       """;

        AssertFormat(source, expected);
    }

    [Test]
    public void BlankLinesBetweenMembersArePreserved() {
        var source = """
                     [using A]


                     [using B]

                     task X
                     {
                     }

                     """;

        AssertFormat(source, source);
    }

    [Test]
    public void BlankLineIsInsertedAfterNamespacePrefix() {
        // Nach dem Top-Level-[namespaceprefix] wird eine Leerzeile erzwungen; die [using] unter sich bleiben tight.
        var source = """
                     [namespaceprefix N]
                     [using A]
                     [using B]
                     """;
        var expected = """
                       [namespaceprefix N]

                       [using A]
                       [using B]

                       """;

        AssertFormat(source, expected);
    }

    [Test]
    public void BlockMembersAreFlankedByBlankLines() {
        // Ein Include unmittelbar vor einem task-Block und zwei direkt aufeinanderfolgende Block-Member
        // bekommen je eine erzwungene Leerzeile (nach '}' bzw. vor dem führenden Schlüsselwort).
        var source = """
                     taskref "A.nav";
                     task X
                     {
                     }
                     task Y
                     {
                     }
                     """;
        var expected = """
                       taskref "A.nav";

                       task X
                       {
                       }

                       task Y
                       {
                       }

                       """;

        AssertFormat(source, expected);
    }

    [Test]
    public void TaskrefWithBodyIsABlockMemberAndGetsFlankedByBlankLines() {
        // Auch die taskref-mit-Body-Form (TaskDeclarationSyntax) ist ein {}-Block-Member und wird geflankt.
        var source = """
                     taskref Ref
                     {
                     }
                     task X
                     {
                     }
                     """;
        var expected = """
                       taskref Ref
                       {
                       }

                       task X
                       {
                       }

                       """;

        AssertFormat(source, expected);
    }

    [Test]
    public void ConsecutiveIncludesStayTightWithoutForcedBlankLine() {
        // Flache Includes (taskref "…";) sind untereinander kein Block-Member -> nur Umbruch, keine Leerzeile.
        var source = """
                     taskref "A.nav";
                     taskref "B.nav";
                     taskref "C.nav";
                     """;
        var expected = """
                       taskref "A.nav";
                       taskref "B.nav";
                       taskref "C.nav";

                       """;

        AssertFormat(source, expected);
    }

    [Test]
    public void ExtraBlankLinesAroundBlockMembersArePreserved() {
        // Das erzwungene Minimum (1) kappt nie nach oben: drei Autoren-Leerzeilen zwischen zwei Blöcken bleiben.
        var source = """
                     task X
                     {
                     }



                     task Y
                     {
                     }

                     """;

        AssertFormat(source, source);
    }

    [Test]
    public void AuthoredLineBreaksInsideStatementsKeepTheirRelativeIndentViaDeltaShift() {
        // Kein Teil-Reflow: eine vom Autor umbrochene Transition wird nie auf eine Zeile gezogen
        // (Renderer-Schranke). Die erste Zeile (I1) sitzt bereits auf dem Block-Einzug (Delta 0) — die
        // Fortsetzungszeile behält daher ihre relative (tiefere) Einrückung (Hand-gelegt-Delta-Shift, S4).
        var source = """
                     task Sample
                     {
                         init I1;
                         exit E;

                         I1
                                 --> E;
                     }

                     """;
        var expected = """
                       task Sample
                       {
                           init    I1;
                           exit    E;

                           I1
                                   --> E;
                       }

                       """;

        AssertFormat(source, expected);
    }

    // ---- TokenPair: Doppelpunkt, Interpunktion, Typ-Interna -------------------------------------

    [Test]
    public void ColonAndPunctuationAreTight() {
        var source = """
                     task Sample
                     {
                         task B I1 ;
                         exit E;

                         I1  :  Out-->E;
                     }
                     """;
        var expected = """
                       task Sample
                       {
                           task    B I1;
                           exit    E;

                           I1:Out --> E;
                       }

                       """;

        AssertFormat(source, expected);
    }

    [Test]
    public void BaseColonGetsSpaceAfterButNodePortStaysTight() {
        var source = """
                     task Sample [base StandardWFS<TS>:IWFServiceBase,IBeginWFSType]
                     {
                         task    Worker w;
                         exit    E;

                         w:Out --> E;
                     }
                     """;
        var expected = """
                       task Sample [base StandardWFS<TS>: IWFServiceBase, IBeginWFSType]
                       {
                           task    Worker w;
                           exit    E;

                           w:Out --> E;
                       }

                       """;

        AssertFormat(source, expected);
    }

    [Test]
    public void TypeInternalsAreTightAndParameterListsGetCommaSpace() {
        var source = """
                     task Sample
                     {
                         init I1 [params List < int > numbers,Dict<string,List<int>>map,T6 [ ] [ ] raw,int ? maybe];
                         exit E;

                         I1 --> E;
                     }
                     """;
        var expected = """
                       task Sample
                       {
                           init    I1 [params List<int> numbers, Dict<string, List<int>> map, T6[][] raw, int? maybe];
                           exit    E;

                           I1 --> E;
                       }

                       """;

        AssertFormat(source, expected);
    }

    // ---- Kommentare & Direktiven ----------------------------------------------------------------

    [Test]
    public void CommentsAreNormalizedButNeverMoved() {
        var source = """
                     task Sample
                     {
                         init I1;      // Start
                           // Banner ----

                         exit E;

                         I1 --> E;// fertig
                     }
                     """;
        var expected = """
                       task Sample
                       {
                           init I1; // Start
                           // Banner ----

                           exit E;

                           I1 --> E; // fertig
                       }

                       """;

        AssertFormat(source, expected);
    }

    [Test]
    public void DirectiveIsResetToColumnZero() {
        var source = """
                         #pragma version 1
                     task A
                     {
                     }

                     """;
        var expected = """
                       #pragma version 1
                       task A
                       {
                       }

                       """;

        AssertFormat(source, expected);
    }

    // ---- Datei-Anfang & Datei-Ende --------------------------------------------------------------
    // Raw-Strings: die Fixtures sind normaler Nav-Code. Newlines am Dateiende (Final-Newline, überzählige
    // End-Leerzeilen) drücken Leerzeilen vor dem schließenden """ aus, deren Fehlen der direkte Abschluss
    // an """ — führender Einzug über zusätzliche Einrückung relativ zu """. Escapt bleibt NUR, wo literale
    // Trailing-Space-*Zeichen* der Prüfgegenstand sind (im .cs-Raw-String unsichtbar und vom Editor/Tooling
    // getilgt) oder die reine-Whitespace-Datei -> leer (kein sinnvoller Raw-String).

    [Test]
    public void LeadingWhitespaceBeforeFirstTokenIsRemoved() {
        var source = """
                        task A
                     {
                     }

                     """;
        var expected = """
                       task A
                       {
                       }

                       """;

        AssertFormat(source, expected);
    }

    [Test]
    public void HeaderCommentIsKeptAtColumnZero() {
        var source = """
                       // Kopf-Kommentar

                     task A
                     {
                     }

                     """;
        var expected = """
                       // Kopf-Kommentar

                       task A
                       {
                       }

                       """;

        AssertFormat(source, expected);
    }

    [Test]
    public void FinalNewlineIsInsertedAndTrailingBlankLinesAreTrimmed() {
        var expected = """
                       task A
                       {
                       }

                       """;

        // Fehlende Final-Newline (Raw-String endet direkt an """) …
        var missingNewline = """
                             task A
                             {
                             }
                             """;

        // … und überzählige End-Leerzeilen (drei Leerzeilen vor """) führen beide auf genau eine Final-Newline.
        var extraBlankLines = """
                              task A
                              {
                              }



                              """;

        AssertFormat(missingNewline,  expected);
        AssertFormat(extraBlankLines, expected);
    }

    [Test]
    public void TrailingCommentAtEndOfFileIsPreserved() {
        // Kommentar am Dateiende ohne Final-Newline -> genau eine wird ergänzt.
        var source = """
                     task A
                     {
                     }
                     // Fußnote
                     """;
        var expected = """
                       task A
                       {
                       }
                       // Fußnote

                       """;

        AssertFormat(source, expected);
    }

    [Test]
    public void CommentOnlyFileIsKeptWithFinalNewline() {
        var expected = """
                       // nur Kommentar

                       """;

        AssertFormat("   // nur Kommentar", expected);
    }

    [Test]
    public void WhitespaceOnlyFileBecomesEmpty() {
        // Escaped: reine-Whitespace-Datei (Trailing-Spaces + Leerzeilen) -> leer; als Raw-String nicht darstellbar.
        AssertFormat("   \r\n\r\n  ", "");
    }

    [Test]
    public void TrailingWhitespaceIsTrimmedOnEveryRewrittenLine() {
        var expected = """
                       task A
                       {
                       }

                       """;

        // Escaped: die Trailing-Spaces hinter "task A" sind der Prüfgegenstand (im Raw-String unsichtbar,
        // würden vom Editor/Tooling getilgt).
        AssertFormat("task A   \r\n{\r\n}\r\n", expected);
    }

    // InsertFinalNewline = false ist von EOF-Trim und Kommentar-/Direktivzeilen-Normalisierung entkoppelt:
    // nur die abschließende Newline entfällt (der Erwartungs-Raw-String endet darum direkt an """), alles
    // Übrige wird trotzdem kanonisiert.
    static readonly NavFormattingOptions NoFinalNewlineOptions = SpacesOptions with { InsertFinalNewline = false };

    [Test]
    public void WithoutInsertFinalNewlineNoTrailingNewlineIsAddedAndBlankLinesAreStillTrimmed() {
        var expected = """
                       task A
                       {
                       }
                       """;

        // Keine End-Newline (Raw-String endet direkt an """) …
        var noNewline = """
                        task A
                        {
                        }
                        """;

        // … eine Final-Newline (eine Leerzeile vor """) …
        var oneNewline = """
                         task A
                         {
                         }

                         """;

        // … und überzählige End-Leerzeilen (drei vor """) führen alle auf genau dieses newline-lose Ergebnis.
        var extraBlankLines = """
                              task A
                              {
                              }



                              """;

        AssertFormat(noNewline,       expected, NoFinalNewlineOptions);
        AssertFormat(oneNewline,      expected, NoFinalNewlineOptions);
        AssertFormat(extraBlankLines, expected, NoFinalNewlineOptions);
    }

    [Test]
    public void WithoutInsertFinalNewlineTrailingCommentIsNormalizedButNotNewlineTerminated() {
        var source = """
                     task A
                     {
                     }
                     // Fußnote

                     """;
        var expected = """
                       task A
                       {
                       }
                       // Fußnote
                       """;

        AssertFormat(source, expected, NoFinalNewlineOptions);
    }

    [Test]
    public void WithoutInsertFinalNewlineTrailingWhitespaceIsStillTrimmed() {
        var expected = """
                       task A
                       {
                       }
                       """;

        // Escaped: die Trailing-Spaces hinter "task A" sind der Prüfgegenstand (im Raw-String unsichtbar).
        AssertFormat("task A   \r\n{\r\n}", expected, NoFinalNewlineOptions);
    }

    // ---- Leerzeilen-Deckel (MaxBlankLines) ------------------------------------------------------
    // Default ist kein Deckel (null) — die „kein Kollaps"-Grundhaltung bleibt der Default; der Deckel ist
    // opt-in und nur ≥ 2 zulässig (Werte darunter werden auf 2 geklemmt, s. MaxBlankLinesBelowTwoIsClampedToTwo).

    static readonly NavFormattingOptions CapOptions = SpacesOptions with { MaxBlankLines = 2 };

    [Test]
    public void MaxBlankLinesBelowTwoIsClampedToTwo() {
        Assert.That(NavFormattingOptions.Default.MaxBlankLines, Is.Null, "Default ist kein Deckel (opt-in).");
        Assert.That((NavFormattingOptions.Default with { MaxBlankLines = 0    }).MaxBlankLines, Is.EqualTo(2));
        Assert.That((NavFormattingOptions.Default with { MaxBlankLines = 1    }).MaxBlankLines, Is.EqualTo(2));
        Assert.That((NavFormattingOptions.Default with { MaxBlankLines = 5    }).MaxBlankLines, Is.EqualTo(5));
        Assert.That((NavFormattingOptions.Default with { MaxBlankLines = null }).MaxBlankLines, Is.Null);
    }

    [Test]
    public void WithoutMaxBlankLinesLargeRunsArePreserved() {
        // Default (null): fünf Autoren-Leerzeilen bleiben stehen (kein Kollaps).
        var source = """
                     task Sample
                     {
                         init    I1;
                         exit    E;

                         I1 --> E;




                         I1 --> E;
                     }

                     """;

        AssertFormat(source, source);
    }

    [Test]
    public void MaxBlankLinesCapsRunsInsideTheBody() {
        // Fünf Leerzeilen zwischen zwei Transitionen -> auf den Deckel (2) gekappt.
        var source = """
                     task Sample
                     {
                         init    I1;
                         exit    E;

                         I1 --> E;




                         I1 --> E;
                     }

                     """;
        var expected = """
                       task Sample
                       {
                           init    I1;
                           exit    E;

                           I1 --> E;


                           I1 --> E;
                       }

                       """;

        AssertFormat(source, expected, CapOptions);
    }

    [Test]
    public void MaxBlankLinesTreatsCommentLinesAsRunResetNotBlank() {
        // Grenzfall „Leerzeilen + Kommentarzeile + Leerzeilen": die Kommentarzeile zählt nicht als
        // Leerzeile und setzt den Lauf zurück -> jeder der beiden Läufe wird eigenständig auf 2 gekappt.
        var source = """
                     task Sample
                     {
                         init    I1;
                         exit    E;

                         I1 --> E;



                         // Ende



                         I1 --> E;
                     }

                     """;
        var expected = """
                       task Sample
                       {
                           init    I1;
                           exit    E;

                           I1 --> E;


                           // Ende


                           I1 --> E;
                       }

                       """;

        AssertFormat(source, expected, CapOptions);
    }

    [Test]
    public void MaxBlankLinesCapsRunsBetweenTrailingCommentsAtFileEnd() {
        // Leerzeilen-Läufe zwischen Kommentarzeilen am Dateiende werden ebenfalls gekappt (drei -> zwei);
        // der EOF-Trailing-Trim (Leerzeilen hinter dem letzten Inhalt) läuft davon unabhängig.
        var source = """
                     task A
                     {
                     }
                     // erste



                     // zweite
                     """;
        var expected = """
                       task A
                       {
                       }
                       // erste


                       // zweite

                       """;

        AssertFormat(source, expected, CapOptions);
    }

    [Test]
    public void MaxBlankLinesCapsLeadingBlankLinesAtFileStart() {
        // Führende Leerzeilen am Dateianfang unterliegen demselben Deckel (drei -> zwei). Escaped: führende
        // Leerzeilen sind der Prüfgegenstand und im Raw-String-Präfix schwer eindeutig darstellbar.
        AssertFormat("\r\n\r\n\r\ntask A\r\n{\r\n}\r\n", "\r\n\r\ntask A\r\n{\r\n}\r\n", CapOptions);
    }

    // ---- Einzugsstil ----------------------------------------------------------------------------

    [Test]
    public void TabsAreTheDefaultIndentStyle() {
        var source = """
                     task A
                     {
                     init I1;
                     }

                     """;

        // Escaped: der erwartete Tab-Einzug ('\t' vor init) ist der Prüfgegenstand und im Raw-String unsichtbar.
        AssertFormat(source, "task A\r\n{\r\n\tinit I1;\r\n}\r\n", NavFormattingOptions.Default);
    }

}
