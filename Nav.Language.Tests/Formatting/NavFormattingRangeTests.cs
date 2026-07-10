#region Using Directives

using System;
using System.Collections.Generic;
using System.Linq;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.Formatting;
using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Nav.Language.Tests.Formatting;

/// <summary>
/// Tests der Selektions-Formatierung (<see cref="NavFormattingService.FormatRange"/>): das tragende Modell
/// <c>FormatRange(x, r) ≡ { c ∈ FormatDocument(x) : c.Extent ⊆ ExpandRange(r) }</c> und seine Folgerungen —
/// Subset-/Monotonie-Garantie, Gleichheit mit <see cref="NavFormattingService.FormatDocument"/> für die
/// ganze Datei, der gemeinsame <c>⊆</c>-Filter des Final-Gaps — sowie das Verhalten bei einer Auswahl
/// mitten im Block, in einem mehrzeiligen <c>[params]</c>, in einem Kommentar und in einer unterdrückten
/// Region.
/// </summary>
[TestFixture]
public class NavFormattingRangeTests {

    static readonly TextEditorSettings Settings = new(tabSize: 4, newLine: "\r\n");

    // Spaces-Einzug, damit die Raw-String-Erwartungen lesbar bleiben (vgl. NavFormattingGoldenTests).
    static readonly NavFormattingOptions SpacesOptions = NavFormattingOptions.Default with { IndentStyle = IndentStyle.Spaces };

    // VerifyResult: Tests schalten den (per Default aus geschalteten) Achse-A-Wächter als Opt-in ein.
    static readonly NavFormattingOptions VerifyingOptions = SpacesOptions with { VerifyResult = true };

    static IReadOnlyList<TextChange> FormatDocument(string text) {
        return NavFormattingService.FormatDocument(SyntaxTree.ParseText(text), Settings, VerifyingOptions);
    }

    static IReadOnlyList<TextChange> FormatRange(string text, TextExtent range) {
        return NavFormattingService.FormatRange(SyntaxTree.ParseText(text), range, Settings, VerifyingOptions);
    }

    static string ApplyRange(string text, TextExtent range) {
        return new TextChangeWriter().ApplyTextChanges(text, FormatRange(text, range));
    }

    /// <summary>Der Extent des ersten Vorkommens von <paramref name="substring"/> in <paramref name="text"/>.</summary>
    static TextExtent RangeOf(string text, string substring) {
        var start = text.IndexOf(substring, StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0), $"Fixture-Marker '{substring}' nicht gefunden.");
        return TextExtent.FromBounds(start, start + substring.Length);
    }

    static TextExtent WholeFile(string text) {
        return TextExtent.FromBounds(0, text.Length);
    }

    // Repräsentative Eingaben für die Eigenschafts-Tests (kanonisch, unaufgeräumt, mehrzeilig, defekt).
    static IEnumerable<string> Fixtures() {

        yield return """
                     task A
                     {
                     }

                     """;

        yield return """
                     task Sample
                     {
                     init I1;
                     exit E;

                     I1-->E;
                     }

                     """;

        yield return """
                     [namespaceprefix N] [using A] [using B] task X
                     {
                     }

                     """;

        yield return """
                     task Sample
                     {
                         init I1;
                         exit E;

                       I1-->E;
                     }

                     """;

        yield return """
                     task Broken
                     {
                         init I1
                         @@@
                         exit E;
                     }

                     """;

        yield return Resources.LargeNav;
    }

    // ---- Modell: Subset / Monotonie / Ganze-Datei-Gleichheit ------------------------------------

    [Test, TestCaseSource(nameof(Fixtures))]
    public void WholeFileRangeEqualsFormatDocument(string text) {

        var range = FormatRange(text, WholeFile(text));
        var full  = FormatDocument(text);

        Assert.That(range, Is.EqualTo(full),
                    "FormatRange über die ganze Datei muss exakt FormatDocument liefern.");
    }

    [Test, TestCaseSource(nameof(Fixtures))]
    public void EveryRangeChangeIsAlsoADocumentChange(string text) {

        var full = FormatDocument(text);

        // Über eine feste Zahl gleichverteilter Auswahl-Fenster (nicht zeichenweise — jeder FormatRange-Aufruf
        // formatiert intern das ganze Dokument inkl. Guard-Reparse): jede von FormatRange gelieferte Änderung
        // muss unverändert (Extent + Ersatztext) auch im Voll-Format vorkommen — die Subset-Garantie.
        const int windows = 12;
        var       step    = Math.Max(1, text.Length / windows);
        var       length  = Math.Max(1, text.Length / 6);

        for (var start = 0; start < text.Length; start += step) {
            var range   = TextExtent.FromBounds(start, Math.Min(text.Length, start + length));
            var partial = FormatRange(text, range);

            Assert.That(partial, Is.SubsetOf(full),
                        $"Jede Range-Änderung muss auch eine Dokument-Änderung sein (Range [{range.Start}-{range.End}]).");
        }
    }

    [Test]
    public void RangeFormatIsIdempotentOverTheSameSelection() {

        // Eine unaufgeräumte Transition wird per Auswahl formatiert; ein zweiter Lauf über dieselbe (nun
        // kanonische) Zeile liefert keine Änderung mehr.
        var source = """
                     task Sample
                     {
                         init I1;
                         exit E;

                       I1-->E;
                     }

                     """;

        var once = ApplyRange(source, RangeOf(source, "I1-->E;"));

        Assert.That(FormatRange(once, RangeOf(once, "I1 --> E;")), Is.Empty,
                    "Der zweite Lauf über dieselbe, nun kanonische Auswahl darf nichts mehr ändern.");
    }

    // ---- Final-Gap unterliegt demselben ⊆-Filter -----------------------------------------------

    [Test]
    public void FinalNewlineIsAddedOnlyWhenTheSelectionReachesTheFileEnd() {

        // Bewusst ohne abschließende Newline (die schließende """-Zeile hängt keine an).
        var source = """
                     task A
                     {
                     }
                     """;

        // Auswahl bis zum Dateiende -> die Final-Newline wird gesetzt (== Voll-Format).
        Assert.That(ApplyRange(source, WholeFile(source)), Is.EqualTo(source + "\r\n"));

        // Auswahl nur der ersten Zeile -> der Final-Gap liegt außerhalb, keine Newline am Ende, und da der
        // Kopf bereits kanonisch ist, bleibt die Datei unverändert.
        Assert.That(ApplyRange(source, RangeOf(source, "task A")), Is.EqualTo(source));
    }

    // ---- Verhalten: Auswahl mitten im Block -----------------------------------------------------

    [Test]
    public void OnlyTheSelectedStatementIsReformattedNeighborsStayRagged() {

        // Node-Grid (init/exit) und die erste Transition sind unaufgeräumt; ausgewählt wird nur die zweite
        // Transition (durch zwei Leerzeilen eine eigene Ausrichtungsgruppe). Nur deren Pfeil-Spacing wird
        // normalisiert — die Nachbarn bleiben unangetastet ("nur die Auswahl anfassen").
        var source = """
                     task Sample
                     {
                         init I1;
                         exit E;

                         I1-->E;


                         I1-->E;
                     }

                     """;

        var expected = """
                       task Sample
                       {
                           init I1;
                           exit E;

                           I1-->E;


                           I1 --> E;
                       }

                       """;

        // Auf das zweite (letzte) Vorkommen der Transition zielen.
        var secondStart = source.LastIndexOf("I1-->E;", StringComparison.Ordinal);
        var range       = TextExtent.FromBounds(secondStart, secondStart + "I1-->E;".Length);

        Assert.That(ApplyRange(source, range), Is.EqualTo(expected));
    }

    [Test]
    public void SelectingAStatementAlsoCorrectsItsIndent() {

        // Die Transition steht auf 2-Spaces-Einzug (soll 4 sein). Die Auswahl der Transitionszeile korrigiert
        // über die vorangehende Lücke auch ihren Einzug — die Node-Deklarationen darüber bleiben ungrid-et.
        var source = """
                     task Sample
                     {
                         init I1;
                         exit E;

                       I1-->E;
                     }

                     """;

        var expected = """
                       task Sample
                       {
                           init I1;
                           exit E;

                           I1 --> E;
                       }

                       """;

        Assert.That(ApplyRange(source, RangeOf(source, "I1-->E;")), Is.EqualTo(expected));
    }

    // ---- Verhalten: Auswahl in einem mehrzeiligen [params] --------------------------------------

    [Test]
    public void SelectingOneLineOfAMultiLineParamsExpandsToTheWholeParamsBlock() {

        // Der Autor hat das [params] mehrzeilig gelegt; die Auswahl trifft nur die zweite Parameterzeile.
        // Die Ausweitung auf den ganzen [params]-Knoten richtet beide Parameter unter dem ersten aus (sonst
        // bliebe die Liste halb formatiert).
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

        Assert.That(ApplyRange(source, RangeOf(source, "string label")), Is.EqualTo(expected));
    }

    // ---- Verhalten: Auswahl in Kommentar / unterdrückter Region ---------------------------------

    [Test]
    public void SelectingInsideACommentIsSafe() {

        var source = """
                     task Sample
                     {
                         init I1;// Kommentar
                         exit E;

                         I1 --> E;
                     }

                     """;

        // Die Auswahl trifft den (als Trailing-Trivia an 'init I1;' hängenden) Kommentar; die Ausweitung
        // erfasst die init-Deklaration. Es entsteht kein Achse-A-Bruch und keine Overlap-Exception — der
        // signifikante Token-Strom bleibt erhalten (nur Whitespace wird angefasst).
        var formatted = ApplyRange(source, RangeOf(source, "// Kommentar"));

        Assert.That(SignificantTokens(formatted), Is.EqualTo(SignificantTokens(source)),
                    "Bedeutungserhalt auch bei Auswahl in einem Kommentar.");
    }

    [Test]
    public void SelectingInsideASuppressedRegionEmitsNoChangeThere() {

        // Fehlendes ';' -> der ganze Task-Body ist verbatim; eine Auswahl darin darf nichts ändern.
        var source = """
                     task Broken
                     {
                         init I1
                         @@@
                         exit E;
                     }

                     """;

        Assert.That(ApplyRange(source, RangeOf(source, "@@@")), Is.EqualTo(source),
                    "In einer unterdrückten Region liefert FormatRange keine Änderung.");
    }

    static IReadOnlyList<(SyntaxTokenType Type, string Text)> SignificantTokens(string text) {
        return SyntaxTree.ParseText(text)
                         .Tokens
                         .Where(token => token.Type != SyntaxTokenType.EndOfFile)
                         .Select(token => (token.Type, token.ToString()))
                         .ToList();
    }

}