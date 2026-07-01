#region Using Directives

using System.Linq;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Nav.Language.Tests;

/// <summary>
/// Prüft die Sprach-Versionierung über <c>#pragma version</c>: Erkennung, Default-Verhalten, die
/// Nav3002-Diagnose bei fehlerhaftem Versionswert sowie die Abgrenzung zu anderen (weiterhin per
/// Nav3000 gemeldeten) Präprozessor-Direktiven.
/// </summary>
[TestFixture]
public class LanguageVersionTests {

    static CodeGenerationUnitSyntax Parse(string text) {
        return (CodeGenerationUnitSyntax) SyntaxTree.ParseText(text).Root;
    }

    [Test]
    public void WellFormedPragma_SetsLanguageVersion() {

        var unit = Parse("#pragma version 2\r\ntask A { init I1; exit e1; I1 --> e1; }");

        Assert.That(unit.LanguageVersionDirective, Is.Not.Null);
        Assert.That(unit.LanguageVersion.Value,    Is.EqualTo(2));
        Assert.That(unit.SyntaxTree.Diagnostics,   Is.Empty);
        Assert.That(unit.SyntaxTree.Directives().OfType<VersionDirectiveSyntax>().Count(), Is.EqualTo(1));
    }

    [Test]
    public void NoPragma_DefaultsToVersionOne() {

        var unit = Parse("task A { init I1; exit e1; I1 --> e1; }");

        Assert.That(unit.LanguageVersionDirective, Is.Null);
        Assert.That(unit.LanguageVersion,          Is.EqualTo(NavLanguageVersion.Default));
        Assert.That(unit.LanguageVersion.Value,    Is.EqualTo(1));
    }

    [Test]
    public void LeadingTriviaBeforePragma_IsAllowed() {

        var unit = Parse("// header\n\n#pragma version 3\r\ntask A { init I1; exit e1; I1 --> e1; }");

        Assert.That(unit.LanguageVersionDirective, Is.Not.Null);
        Assert.That(unit.LanguageVersion.Value,    Is.EqualTo(3));
    }

    [Test]
    public void MissingVersionNumber_ReportsNav3002_AndDefaults() {

        var unit = Parse("#pragma version\r\ntask A { init I1; exit e1; I1 --> e1; }");

        Assert.That(unit.LanguageVersionDirective, Is.Not.Null);
        Assert.That(unit.LanguageVersion.Value,    Is.EqualTo(1));

        var ids = unit.SyntaxTree.Diagnostics.Select(d => d.Descriptor.Id).ToList();
        Assert.That(ids, Does.Contain("Nav3002"));
        Assert.That(ids, Does.Not.Contain("Nav3000"));
    }

    [Test]
    public void NonIntegerVersion_ReportsNav3002() {

        var unit = Parse("#pragma version abc\r\ntask A { init I1; exit e1; I1 --> e1; }");

        Assert.That(unit.LanguageVersionDirective,                          Is.Not.Null);
        Assert.That(unit.LanguageVersion.Value,                             Is.EqualTo(1));
        Assert.That(unit.SyntaxTree.Diagnostics.Count(d => d.Descriptor.Id == "Nav3002"), Is.EqualTo(1));
    }

    [Test]
    public void OtherPragma_IsNotRecognized_AndStillReportsNav3000() {

        var unit = Parse("#pragma warning disable Nav1234\r\ntask A { init I1; exit e1; I1 --> e1; }");

        Assert.That(unit.LanguageVersionDirective, Is.Null);
        Assert.That(unit.LanguageVersion.Value,    Is.EqualTo(1));

        var ids = unit.SyntaxTree.Diagnostics.Select(d => d.Descriptor.Id).ToList();
        Assert.That(ids, Does.Contain("Nav3000"));
    }

    [Test]
    public void MisplacedPragma_AfterMember_ReportsNav3003_AndDefaults() {

        var unit = Parse("task A { init I1; exit e1; I1 --> e1; }\r\n#pragma version 2");

        // Nicht ganz oben ⇒ unwirksam (Default) und als Nav3003 gemeldet — nicht als Nav3000.
        Assert.That(unit.LanguageVersionDirective, Is.Null);
        Assert.That(unit.LanguageVersion.Value,    Is.EqualTo(1));

        var ids = unit.SyntaxTree.Diagnostics.Select(d => d.Descriptor.Id).ToList();
        Assert.That(ids, Does.Contain("Nav3003"));
        Assert.That(ids, Does.Not.Contain("Nav3000"));
        Assert.That(ids, Does.Not.Contain("Nav3002"));
    }

    [Test]
    public void PragmaAfterOtherDirective_ReportsNav3003() {

        var unit = Parse("#pragma warning disable Nav1\r\n#pragma version 2\r\ntask A { init I1; exit e1; I1 --> e1; }");

        // Vor der Versions-Direktive steht eine andere Direktive (kein Trivia) ⇒ nicht ganz oben (Nav3003);
        // die vorangehende Direktive bleibt eine nicht erkannte (Nav3000).
        Assert.That(unit.LanguageVersionDirective, Is.Null);
        Assert.That(unit.LanguageVersion.Value,    Is.EqualTo(1));

        var ids = unit.SyntaxTree.Diagnostics.Select(d => d.Descriptor.Id).ToList();
        Assert.That(ids, Does.Contain("Nav3003"));
        Assert.That(ids, Does.Contain("Nav3000"));
    }

    [Test]
    public void DuplicatePragma_ReportsNav3004_FirstWins() {

        var unit = Parse("#pragma version 2\r\n#pragma version 3\r\ntask A { init I1; exit e1; I1 --> e1; }");

        // Die erste (wirksame) Versions-Direktive bestimmt die Version; die zweite ist ein Duplikat (Nav3004)
        // und wird kein eigener Knoten.
        Assert.That(unit.LanguageVersionDirective, Is.Not.Null);
        Assert.That(unit.LanguageVersion.Value,    Is.EqualTo(2));
        Assert.That(unit.SyntaxTree.Directives().OfType<VersionDirectiveSyntax>().Count(), Is.EqualTo(1));

        var ids = unit.SyntaxTree.Diagnostics.Select(d => d.Descriptor.Id).ToList();
        Assert.That(ids, Does.Contain("Nav3004"));
        Assert.That(ids, Does.Not.Contain("Nav3000"));
        Assert.That(ids, Does.Not.Contain("Nav3003"));
    }

    [Test]
    public void DuplicatePragma_AfterMember_ReportsNav3003_NotNav3004() {

        var unit = Parse("#pragma version 2\r\ntask A { init I1; exit e1; I1 --> e1; }\r\n#pragma version 3");

        // Die zweite Direktive steht hinter echtem Code: die Deplatzierung (Nav3003) ist das eigentliche
        // Problem und verdrängt die andernfalls nur lärmende Duplikat-Meldung (Nav3004). Die erste
        // (wirksame) Direktive bestimmt weiterhin die Version.
        Assert.That(unit.LanguageVersionDirective, Is.Not.Null);
        Assert.That(unit.LanguageVersion.Value,    Is.EqualTo(2));
        Assert.That(unit.SyntaxTree.Directives().OfType<VersionDirectiveSyntax>().Count(), Is.EqualTo(1));

        var ids = unit.SyntaxTree.Diagnostics.Select(d => d.Descriptor.Id).ToList();
        Assert.That(ids, Does.Contain("Nav3003"));
        Assert.That(ids, Does.Not.Contain("Nav3004"));
    }

    [Test]
    public void DirectiveDiagnostic_SpansWholeDirective_NotJustTheHash() {

        // Die Squiggle soll die ganze Direktive markieren (# … Versionswert), nicht nur das '#'.
        var source = "task A { init I1; exit e1; I1 --> e1; }\r\n#pragma version 2";
        var unit   = Parse(source);

        var diagnostic = unit.SyntaxTree.Diagnostics.Single(d => d.Descriptor.Id == "Nav3003");

        var start = source.IndexOf("#pragma", System.StringComparison.Ordinal);
        Assert.That(diagnostic.Location.Start,  Is.EqualTo(start));
        Assert.That(diagnostic.Location.Length, Is.EqualTo("#pragma version 2".Length));

        // Auch die Zeilen-Range muss die volle Breite tragen (nicht nullbreit) — sonst zieht VS Code die
        // Squiggle nur über ein Zeichen, obwohl der Extent stimmt.
        Assert.That(diagnostic.Location.StartLine, Is.EqualTo(diagnostic.Location.EndLine));
        Assert.That(diagnostic.Location.EndCharacter - diagnostic.Location.StartCharacter,
                    Is.EqualTo("#pragma version 2".Length));
    }

    [Test]
    public void MidLinePragma_AfterCode_IsNotADirective_ReportsNav0000() {

        // Ein '#' mitten in der Zeile (hinter Code) beginnt keine Direktive: der Lexer erzeugt dafür ein
        // einzelnes unbekanntes Zeichen (Nav0000), der Zeilenrest lext gewöhnlich weiter. Es entsteht daher
        // keine zweite Versions-Direktive und kein Nav3003/Nav3004 — nur die erste, wohlplatzierte Direktive
        // bleibt wirksam.
        var source = "#pragma version 1\r\ntask A { init I1; exit e1; I1 --> e1; } #pragma version 1";
        var unit   = Parse(source);

        Assert.That(unit.LanguageVersion.Value, Is.EqualTo(1));
        Assert.That(unit.SyntaxTree.Directives().OfType<VersionDirectiveSyntax>().Count(), Is.EqualTo(1));

        var ids = unit.SyntaxTree.Diagnostics.Select(d => d.Descriptor.Id).ToList();
        Assert.That(ids, Does.Contain("Nav0000"));
        Assert.That(ids, Does.Not.Contain("Nav3003"));
        Assert.That(ids, Does.Not.Contain("Nav3004"));

        // Das unbekannte '#' sitzt an seiner mittigen Position, nicht am Zeilenanfang.
        var hashPosition = source.LastIndexOf('#');
        var nav0000      = unit.SyntaxTree.Diagnostics.Single(d => d.Descriptor.Id == "Nav0000");
        Assert.That(nav0000.Location.Start, Is.EqualTo(hashPosition));
    }

    [Test]
    public void DuplicatePragma_BothAtTop_ReportsNav3004_SpanningWholeDirective() {

        // Zwei Versions-Direktiven, beide am Kopf (jede am Zeilenanfang, nur Trivia dazwischen): die zweite
        // ist ein echtes Duplikat (Nav3004). Die Squiggle soll die ganze zweite Direktive markieren
        // (# … Versionswert), nicht nur das '#'.
        var source = "#pragma version 1\r\n#pragma version 1\r\ntask A { init I1; exit e1; I1 --> e1; }";
        var unit   = Parse(source);

        var diagnostic = unit.SyntaxTree.Diagnostics.Single(d => d.Descriptor.Id == "Nav3004");
        var start      = source.LastIndexOf("#pragma", System.StringComparison.Ordinal);

        Assert.That(diagnostic.Location.Start,  Is.EqualTo(start));
        Assert.That(diagnostic.Location.Length, Is.EqualTo("#pragma version 1".Length));

        // Auch die Zeilen-Range muss die volle Breite tragen (nicht nullbreit).
        Assert.That(diagnostic.Location.StartLine, Is.EqualTo(diagnostic.Location.EndLine));
        Assert.That(diagnostic.Location.EndCharacter - diagnostic.Location.StartCharacter,
                    Is.EqualTo("#pragma version 1".Length));
    }

    // Die Token einer Direktive liegen nicht mehr im flachen Strom, sondern lokal am Direktiv-Knoten
    // (strukturierte Trivia) — erreichbar über die Trivia.
    static System.Collections.Generic.IEnumerable<SyntaxToken> DirectiveTokens(SyntaxTree tree) {
        return tree.Directives().SelectMany(directive => directive.ChildTokens());
    }

    [Test]
    public void MisplacedPragma_StillClassifiesNumberValue() {

        // Auch eine deplatzierte Versions-Direktive (Nav3003) behält die Färbung ihrer Rumpf-Token: der Wert
        // wird weiterhin als Zahl klassifiziert, obwohl die Direktive unwirksam ist.
        var tree = SyntaxTree.ParseText("task A { init I1; exit e1; I1 --> e1; }\r\n#pragma version 7");

        Assert.That(DirectiveTokens(tree).Any(t => t.Classification == TextClassification.NumberLiteral), Is.True);
    }

    [Test]
    public void WellFormedPragma_ClassifiesVersionKeywordAndNumber() {

        // Positionen in "#pragma version 12": '#'=0, 'pragma'=1..6, ' '=7, 'version'=8..14, ' '=15, '12'=16..17.
        var tree   = SyntaxTree.ParseText("#pragma version 12\r\ntask A { init I1; exit e1; I1 --> e1; }");
        var tokens = DirectiveTokens(tree).ToList();

        TextClassification At(int position) {
            return tokens.Single(t => t.Extent.Contains(TextExtent.FromBounds(position, position + 1))).Classification;
        }

        Assert.That(At(0),  Is.EqualTo(TextClassification.PreprocessorKeyword), "'#'");
        Assert.That(At(1),  Is.EqualTo(TextClassification.PreprocessorKeyword), "'pragma'");
        Assert.That(At(8),  Is.EqualTo(TextClassification.PreprocessorKeyword), "Anfang von 'version'");
        Assert.That(At(14), Is.EqualTo(TextClassification.PreprocessorKeyword), "Ende von 'version'");
        Assert.That(At(16), Is.EqualTo(TextClassification.NumberLiteral),       "erste Ziffer von '12'");
        Assert.That(At(17), Is.EqualTo(TextClassification.NumberLiteral),       "zweite Ziffer von '12'");
    }

    [Test]
    public void InvalidVersion_DoesNotClassifyAsNumber() {

        // 'version' bleibt Präprozessor-Schlüsselwort, aber der ungültige Wert wird nicht als Zahl gefärbt.
        var tree = SyntaxTree.ParseText("#pragma version abc\r\ntask A { init I1; exit e1; I1 --> e1; }");

        Assert.That(DirectiveTokens(tree).Any(t => t.Classification == TextClassification.NumberLiteral), Is.False);
    }

    [Test]
    public void WellFormedPragma_RoundTrips() {

        var source = "#pragma version 2\r\ntask A { init I1; exit e1; I1 --> e1; }";
        var tree   = SyntaxTree.ParseText(source);

        var sb = new System.Text.StringBuilder();
        foreach (var token in tree.Tokens) {
            foreach (var trivia in token.LeadingTrivia) {
                sb.Append(trivia.ToString(tree.SourceText));
            }

            sb.Append(token.ToString());

            foreach (var trivia in token.TrailingTrivia) {
                sb.Append(trivia.ToString(tree.SourceText));
            }
        }

        Assert.That(sb.ToString(), Is.EqualTo(source));
    }

    [TestCase("1",   true,  1)]
    [TestCase("2",   true,  2)]
    [TestCase("10",  true,  10)]
    [TestCase(" 3 ", true,  3)]
    [TestCase("",    false, 0)]
    [TestCase("x",   false, 0)]
    [TestCase("-1",  false, 0)]
    [TestCase("1.0", false, 0)]
    public void TryParse_HandlesInputs(string text, bool expectedOk, int expectedValue) {

        var ok = NavLanguageVersion.TryParse(text, out var version);

        Assert.That(ok, Is.EqualTo(expectedOk));
        if (expectedOk) {
            Assert.That(version.Value, Is.EqualTo(expectedValue));
        }
    }

}
