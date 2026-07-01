#region Using Directives

using System.Linq;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;

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
