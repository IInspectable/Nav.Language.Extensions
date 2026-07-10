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
        yield return "task A\r\n{\r\n}\r\n";
        yield return "task Sample\r\n{\r\n\tinit I1;\r\n\texit E;\r\n\r\n\tI1 --> E;\r\n}\r\n";
        yield return "#pragma version 1\r\ntask A\r\n{\r\n}\r\n";
        yield return "// Kopf-Kommentar\r\ntask A   \r\n{\r\n  init I1;   exit E;\r\n\r\n\r\n  I1-->E;// tail\r\n}";
        yield return "task Broken\r\n{\r\n    init I1\r\n    @@@\r\n    exit E;\r\n}\r\n";
        yield return Resources.LargeNav;
    }

    [Test]
    public void EmptyFileProducesNoChanges() {
        Assert.That(Format(""), Is.Empty);
    }

    [Test]
    public void TrivialCanonicalFileProducesNoChanges() {
        Assert.That(Format("task A\r\n{\r\n}\r\n"), Is.Empty);
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

    static IReadOnlyList<(SyntaxTokenType Type, string Text)> SignificantTokens(string text) {
        return SyntaxTree.ParseText(text)
                         .Tokens
                         .Where(token => token.Type != SyntaxTokenType.EndOfFile)
                         .Select(token => (token.Type, token.ToString()))
                         .ToList();
    }

}
