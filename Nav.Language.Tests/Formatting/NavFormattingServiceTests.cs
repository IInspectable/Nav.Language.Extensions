#region Using Directives

using System.Collections.Generic;
using System.Linq;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.Formatting;
using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Nav.Language.Tests.Formatting;

/// <summary>
/// End-to-End-Eigenschaften des <see cref="NavFormattingService"/>: bereits kanonische Dateien bleiben
/// unangetastet (0 Changes), die Ein-Change-pro-Lücke-Invariante hält (disjunkte, geordnete Changes),
/// der Formatter ist idempotent und erhält den signifikanten Token-Strom (Bedeutungserhalt) — die
/// dauerhaften Korrektheits-Eigenschaften, unabhängig vom jeweils aktiven Regelsatz.
/// </summary>
[TestFixture]
public class NavFormattingServiceTests {

    static readonly TextEditorSettings Settings = new(tabSize: 4, newLine: "\r\n");

    static IReadOnlyList<TextChange> Format(string text) {
        return NavFormattingService.FormatDocument(SyntaxTree.ParseText(text), Settings, NavFormattingOptions.Default);
    }

    static string ApplyFormat(string text) {
        return new TextChangeWriter().ApplyTextChanges(text, Format(text));
    }

    // Eingaben für die dauerhaften Eigenschafts-Tests: von leer über kanonisch bis unaufgeräumt/defekt.
    static IEnumerable<string> Fixtures() {
        yield return "";
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

            I1 --> E;
        }
        """;
        yield return """
        #pragma version 1
        task A
        {
        }
        """;
        // Bewusst unaufgeräumt/defekt: Trailing-Whitespace ('task A   '), enge Zwischenräume und ein
        // fehlendes Datei-Endzeichen bleiben als escapte Literale sichtbar (im Raw-String wären die
        // Trailing-Spaces unsichtbar und würden vom Editor/Tooling getilgt).
        yield return "// Kopf-Kommentar\r\ntask A   \r\n{\r\n  init I1;   exit E;\r\n\r\n\r\n  I1-->E;// tail\r\n}";
        yield return """
        task Broken
        {
            init I1
            @@@
            exit E;
        }
        """;
        // S4-Fehler-Toleranz: fehlendes ';'/'}', Skiped im Statement, Streu-Token zwischen Membern, BOM,
        // Global-Fallback, Hand-gelegt-Delta-Shift, mehrzeiliger Block-Kommentar.
        yield return """
        task Sample
        {
            init I1;
            exit E;

            A  -->  B
            B --> E;
        }

        """;
        yield return """
        task Good
        {
        init I1;
        I1 --> E;
        }
        task Broken
        {
            init   X;
            X  -->  E;

        """;
        yield return """
        [using A]
        @@@
        [using B]
        task X
        {
        }

        """;
        // Diese beiden bleiben bewusst escapte Literale: das führende BOM (U+FEFF) und der reine
        // Einzeiler ließen sich als Raw-String nur schlechter bzw. unsichtbar darstellen.
        yield return "﻿task A\r\n{\r\n}\r\n";
        yield return "@@@ %%% &&&\r\n";
        yield return """
        task Sample
        {
            init I1;
            exit E;

          A
              --> E;
        }

        """;
        yield return """
        task Sample
        {
              /* Zeile1
                 Zeile2 */
            init I1;
        }

        """;
        yield return Resources.LargeNav;
    }

    [Test]
    public void EmptyFileProducesNoChanges() {
        Assert.That(Format(""), Is.Empty);
    }

    [Test]
    public void TrivialCanonicalFileProducesNoChanges() {
        Assert.That(Format("""
        task A
        {
        }

        """), Is.Empty);
    }

    [Test, TestCaseSource(nameof(Fixtures))]
    public void ChangesAreDisjointAndOrdered(string text) {

        var changes = Format(text);

        var currentEnd = 0;
        foreach (var change in changes) {
            Assert.That(change.Extent.Start, Is.GreaterThanOrEqualTo(currentEnd),
                        "Ein-Change-pro-Lücke: die Change-Extents müssen paarweise disjunkt und geordnet sein.");
            currentEnd = change.Extent.End;
        }
    }

    [Test, TestCaseSource(nameof(Fixtures))]
    public void FormatIsIdempotent(string text) {

        var once  = ApplyFormat(text);
        var twice = ApplyFormat(once);

        Assert.That(twice, Is.EqualTo(once), "format(format(x)) muss format(x) sein.");
        Assert.That(Format(once), Is.Empty, "Der zweite Lauf darf keine Changes mehr liefern.");
    }

    [Test, TestCaseSource(nameof(Fixtures))]
    public void FormatPreservesSignificantTokenStream(string text) {

        var formatted = ApplyFormat(text);

        var before = SignificantTokens(text);
        var after  = SignificantTokens(formatted);

        Assert.That(after, Is.EqualTo(before),
                    "Bedeutungserhalt: format(x) muss zum identischen signifikanten Token-Strom (Typ + Text) zurück-parsen.");
    }

    [Test, TestCaseSource(nameof(Fixtures))]
    public void FormatPreservesDirectiveSequenceAndAddsNoErrors(string text) {

        var formatted = ApplyFormat(text);

        var before = SyntaxTree.ParseText(text);
        var after  = SyntaxTree.ParseText(formatted);

        Assert.That(after.Directives().Select(d => d.ToString()),
                    Is.EqualTo(before.Directives().Select(d => d.ToString()).ToList()),
                    "Achse A: die Direktiv-Sequenz (Typ + Text) muss erhalten bleiben (Direktiven leben in Trivia).");

        Assert.That(ErrorCount(after), Is.LessThanOrEqualTo(ErrorCount(before)),
                    "Achse A: die Formatierung darf keine neuen Error-Diagnostics einführen.");
    }

    static int ErrorCount(SyntaxTree syntaxTree) {
        return syntaxTree.Diagnostics.Count(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    static IReadOnlyList<(SyntaxTokenType Type, string Text)> SignificantTokens(string text) {
        return SyntaxTree.ParseText(text)
                         .Tokens
                         .Where(token => token.Type != SyntaxTokenType.EndOfFile)
                         .Select(token => (token.Type, token.ToString()))
                         .ToList();
    }

}
