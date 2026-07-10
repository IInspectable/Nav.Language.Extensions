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
        var changes = NavFormattingService.FormatDocument(SyntaxTree.ParseText(text), Settings, options ?? SpacesOptions);
        return new TextChangeWriter().ApplyTextChanges(text, changes);
    }

    static void AssertFormat(string source, string expected, NavFormattingOptions options = null) {
        Assert.That(Format(source, options), Is.EqualTo(expected));
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
        """ + "\r\n";

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
        """ + "\r\n";

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
        """ + "\r\n";

        AssertFormat(source, source);
    }

    [Test]
    public void MemberBreaksPutEveryTopLevelMemberOnItsOwnLine() {
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
        """ + "\r\n";

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
        """ + "\r\n";

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
        """ + "\r\n";
        var expected = """
        task Sample
        {
            init    I1;
            exit    E;

            I1
                    --> E;
        }
        """ + "\r\n";

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
        """ + "\r\n";

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
        """ + "\r\n";

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
        """ + "\r\n";

        AssertFormat(source, expected);
    }

    [Test]
    public void DirectiveIsResetToColumnZero() {
        var source = "    #pragma version 1\r\ntask A\r\n{\r\n}\r\n";
        var expected = """
        #pragma version 1
        task A
        {
        }
        """ + "\r\n";

        AssertFormat(source, expected);
    }

    // ---- Datei-Anfang & Datei-Ende --------------------------------------------------------------

    [Test]
    public void LeadingWhitespaceBeforeFirstTokenIsRemoved() {
        AssertFormat("   task A\r\n{\r\n}\r\n", "task A\r\n{\r\n}\r\n");
    }

    [Test]
    public void HeaderCommentIsKeptAtColumnZero() {
        var source = """
          // Kopf-Kommentar

        task A
        {
        }
        """ + "\r\n";
        var expected = """
        // Kopf-Kommentar

        task A
        {
        }
        """ + "\r\n";

        AssertFormat(source, expected);
    }

    [Test]
    public void FinalNewlineIsInsertedAndTrailingBlankLinesAreTrimmed() {
        AssertFormat("task A\r\n{\r\n}", "task A\r\n{\r\n}\r\n");
        AssertFormat("task A\r\n{\r\n}\r\n\r\n\r\n", "task A\r\n{\r\n}\r\n");
    }

    [Test]
    public void TrailingCommentAtEndOfFileIsPreserved() {
        AssertFormat("task A\r\n{\r\n}\r\n// Fußnote", "task A\r\n{\r\n}\r\n// Fußnote\r\n");
    }

    [Test]
    public void CommentOnlyFileIsKeptWithFinalNewline() {
        AssertFormat("   // nur Kommentar", "// nur Kommentar\r\n");
    }

    [Test]
    public void WhitespaceOnlyFileBecomesEmpty() {
        AssertFormat("   \r\n\r\n  ", "");
    }

    [Test]
    public void TrailingWhitespaceIsTrimmedOnEveryRewrittenLine() {
        AssertFormat("task A   \r\n{\r\n}\r\n", "task A\r\n{\r\n}\r\n");
    }

    // ---- Einzugsstil ----------------------------------------------------------------------------

    [Test]
    public void TabsAreTheDefaultIndentStyle() {
        AssertFormat("task A\r\n{\r\ninit I1;\r\n}\r\n",
                     "task A\r\n{\r\n\tinit I1;\r\n}\r\n",
                     NavFormattingOptions.Default);
    }

}
