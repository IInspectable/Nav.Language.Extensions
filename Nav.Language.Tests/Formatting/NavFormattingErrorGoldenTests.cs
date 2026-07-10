#region Using Directives

using NUnit.Framework;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.Formatting;
using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Nav.Language.Tests.Formatting;

/// <summary>
/// Golden-Tests der S4-Fehler-Toleranz: unterdrückte (verbatim) Regionen bei Strukturbrüchen (fehlendes
/// <c>;</c>, fehlendes <c>}</c>, Skiped-Läufe, Streu-Token, BOM), der Hand-gelegt-Delta-Shift (äußerer
/// Einzug mehrzeiliger Anweisungen) und der gleichgebaute Delta-Shift der Innenzeilen mehrzeiliger
/// <c>/* */</c>-Block-Kommentare — jeweils mit Idempotenz-Prüfung. Der Laufzeit-Wächter (Achse A) sichert
/// zusätzlich pro Aufruf ab, dass keine dieser Umformungen den Token-Strom verändert.
/// </summary>
[TestFixture]
public class NavFormattingErrorGoldenTests {

    static readonly TextEditorSettings Settings = new(tabSize: 4, newLine: "\r\n");

    static readonly NavFormattingOptions SpacesOptions = NavFormattingOptions.Default with { IndentStyle = IndentStyle.Spaces };

    static string Format(string text) {
        var changes = NavFormattingService.FormatDocument(SyntaxTree.ParseText(text), Settings, SpacesOptions);
        return new TextChangeWriter().ApplyTextChanges(text, changes);
    }

    static void AssertFormat(string source, string expected) {
        Assert.That(Format(source), Is.EqualTo(expected));
        Assert.That(Format(expected), Is.EqualTo(expected), "Das Golden selbst muss ein Fixpunkt sein (Idempotenz).");
    }

    // ---- Unterdrückung: fehlende Struktur-Token -------------------------------------------------

    [Test]
    public void MissingSemicolonKeepsStatementInnerVerbatimButFormatsNeighbors() {
        // Die ';'-lose Transition bleibt innen verbatim (die doppelten Spaces bleiben) und fällt aus der
        // Pfeil-Ausrichtung; ihre intakten Nachbarn werden normal formatiert.
        var source = """
        task Sample
        {
            init I1;
            exit E;

            A  -->  B
            B --> E;
        }
        """ + "\r\n";
        var expected = """
        task Sample
        {
            init    I1;
            exit    E;

            A  -->  B
            B --> E;
        }
        """ + "\r\n";

        AssertFormat(source, expected);
    }

    [Test]
    public void MissingCloseBraceKeepsTaskBodyVerbatimButFormatsOtherMembers() {
        // Der ganze Body des Tasks mit fehlendem '}' bleibt verbatim (X-Deklaration/Transition mit
        // odd-Spacing bleiben) und es wird kein '}' erfunden; der vollständige Nachbar-Task wird formatiert.
        var source = "task Good\r\n{\r\ninit I1;\r\nI1 --> E;\r\n}\r\ntask Broken\r\n{\r\n    init   X;\r\n    X  -->  E;\r\n";
        var expected = """
        task Good
        {
            init I1;

            I1 --> E;
        }
        task Broken
        {
            init   X;
            X  -->  E;
        """ + "\r\n";

        AssertFormat(source, expected);
    }

    // ---- Unterdrückung: Skiped-Läufe ------------------------------------------------------------

    [Test]
    public void SkippedTokensInsideAStatementKeepItVerbatim() {
        var source = """
        task Sample
        {
            init I1;
            exit E;

            A  @@@  --> B;
            B --> E;
        }
        """ + "\r\n";
        var expected = """
        task Sample
        {
            init    I1;
            exit    E;

            A  @@@  --> B;
            B --> E;
        }
        """ + "\r\n";

        AssertFormat(source, expected);
    }

    [Test]
    public void StrayTokensBetweenMembersOnlyFreezeThatOneGap() {
        // Der Streu-Lauf zwischen zwei Membern (kein gemeinsamer Anweisungs-Elter) lässt nur seine eine
        // Lücke verbatim; die Nachbarn werden normal formatiert.
        var source = """
        [using A]
        @@@
        [using B]
        task X
        {
        }
        """ + "\r\n";

        AssertFormat(source, source);
    }

    // ---- BOM-Guard ------------------------------------------------------------------------------

    [Test]
    public void ByteOrderMarkAtOffsetZeroIsPreserved() {
        AssertFormat("﻿task A\r\n{\r\n}\r\n", "﻿task A\r\n{\r\n}\r\n");
    }

    // ---- Global-Fallback ------------------------------------------------------------------------

    [Test]
    public void GarbageWithoutUsableMembersIsLeftUntouched() {
        // Keine brauchbaren Member -> nur die konservativen Rand-Lücken; der Müll bleibt byte-genau.
        AssertFormat("@@@ %%% &&&\r\n", "@@@ %%% &&&\r\n");
    }

    // ---- Hand-gelegt-Delta-Shift ----------------------------------------------------------------

    [Test]
    public void HandLaidStatementIsReindentedPreservingItsRelativeForm() {
        // Die erste Zeile (A) wird vom Autor-Einzug 2 auf den Block-Einzug 4 re-gesetzt (Delta +2); die
        // Fortsetzungszeile wird um dasselbe Delta mitgeschoben (6 -> 8), relative Form bleibt erhalten.
        var source = """
        task Sample
        {
            init I1;
            exit E;

          A
              --> E;
        }
        """ + "\r\n";
        var expected = """
        task Sample
        {
            init    I1;
            exit    E;

            A
                --> E;
        }
        """ + "\r\n";

        AssertFormat(source, expected);
    }

    // ---- Delta-Shift mehrzeiliger Block-Kommentare ----------------------------------------------

    [Test]
    public void MultiLineBlockCommentInteriorIsDeltaShiftedNotReflowed() {
        // Kein Reflow des Kommentar-Inneren; die erste Zeile wandert von Spalte 6 auf den Block-Einzug 4
        // (Delta -2), die Folgezeile wird um dasselbe Delta mitgeschoben (9 -> 7).
        var source = "task Sample\r\n{\r\n      /* Zeile1\r\n         Zeile2 */\r\n    init I1;\r\n}\r\n";
        var expected = """
        task Sample
        {
            /* Zeile1
               Zeile2 */
            init I1;
        }
        """ + "\r\n";

        AssertFormat(source, expected);
    }

}
