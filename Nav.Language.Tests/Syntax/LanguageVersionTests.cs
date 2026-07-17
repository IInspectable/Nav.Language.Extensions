#region Using Directives

using System.Linq;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Nav.Language.Tests;

/// <summary>
/// Prüft die Sprach-Versionierung über <c>#version</c>: Erkennung, Default-Verhalten, die
/// Nav3002-Diagnose bei fehlerhaftem Versionswert, die Abgrenzung zu unbekannten Pragmas
/// (<c>#pragma …</c> ⇒ Nav3001) und zu sonstigen unbekannten Präprozessor-Direktiven (Nav3000).
/// </summary>
[TestFixture]
public class LanguageVersionTests {

    static CodeGenerationUnitSyntax Parse(string text) {
        return (CodeGenerationUnitSyntax) SyntaxTree.ParseText(text).Root;
    }

    [Test]
    public void WellFormedDirective_SetsLanguageVersion() {

        var unit = Parse(
            """
            #version 2
            task A { init I1; exit e1; I1 --> e1; }
            """);

        Assert.That(unit.LanguageVersionDirective, Is.Not.Null);
        Assert.That(unit.LanguageVersion.Value,    Is.EqualTo(2));
        Assert.That(unit.SyntaxTree.Diagnostics,   Is.Empty);
        Assert.That(unit.SyntaxTree.Directives().OfType<VersionDirectiveSyntax>().Count(), Is.EqualTo(1));
    }

    [Test]
    public void NoDirective_DefaultsToVersionOne() {

        var unit = Parse("task A { init I1; exit e1; I1 --> e1; }");

        Assert.That(unit.LanguageVersionDirective, Is.Null);
        Assert.That(unit.LanguageVersion,          Is.EqualTo(NavLanguageVersion.Default));
        Assert.That(unit.LanguageVersion.Value,    Is.EqualTo(1));
    }

    [Test]
    public void LfTerminatedDirective_TerminatesAtNewline_TaskStaysCode() {

        // Früherer LF-Fallstrick: im Textmodus (hinter dem Keyword) beendete nur '\r\n' die Direktive; ein
        // einzelnes '\n' blieb Rumpf und verschluckte den Rest der Datei. Jetzt terminiert auch das LF
        // zeilengenau — die '#version 2'-Zeile bleibt die Direktive, die folgende Task echter Code.
        var unit = Parse("#version 2\ntask A { init I1; exit e1; I1 --> e1; }");

        Assert.That(unit.LanguageVersionDirective, Is.Not.Null);
        Assert.That(unit.LanguageVersion.Value,    Is.EqualTo(2));
        Assert.That(unit.SyntaxTree.Diagnostics,   Is.Empty);
        Assert.That(unit.TaskDefinitions.Count,    Is.EqualTo(1));
        Assert.That(unit.TaskDefinitions[0].Identifier.ToString(), Is.EqualTo("A"));
    }

    [Test]
    public void LeadingTriviaBeforeDirective_IsAllowed() {

        var unit = Parse("// header\n\n#version 3\r\ntask A { init I1; exit e1; I1 --> e1; }");

        Assert.That(unit.LanguageVersionDirective, Is.Not.Null);
        Assert.That(unit.LanguageVersion.Value,    Is.EqualTo(3));
    }

    [Test]
    public void MissingVersionNumber_ReportsNav3002_AndDefaults() {

        var unit = Parse(
            """
            #version
            task A { init I1; exit e1; I1 --> e1; }
            """);

        Assert.That(unit.LanguageVersionDirective, Is.Not.Null);
        Assert.That(unit.LanguageVersion.Value,    Is.EqualTo(1));

        var ids = unit.SyntaxTree.Diagnostics.Select(d => d.Descriptor.Id).ToList();
        Assert.That(ids, Does.Contain("Nav3002"));
        Assert.That(ids, Does.Not.Contain("Nav3000"));
    }

    [Test]
    public void NonIntegerVersion_ReportsNav3002() {

        var unit = Parse(
            """
            #version abc
            task A { init I1; exit e1; I1 --> e1; }
            """);

        Assert.That(unit.LanguageVersionDirective,                          Is.Not.Null);
        Assert.That(unit.LanguageVersion.Value,                             Is.EqualTo(1));
        Assert.That(unit.SyntaxTree.Diagnostics.Count(d => d.Descriptor.Id == "Nav3002"), Is.EqualTo(1));
    }

    [Test]
    public void TrailingTokens_AfterValidVersion_VersionStands_SurplusIsSkiped() {

        // Die gültige Zahl gilt (Version 2); der überzählige Rest löst genau eine Nav3002 aus und wird
        // ausgegraut (Skiped) — er soll nicht wie ein gültiger Wert (Zahl) aussehen.
        var source = """
            #version 2 xy
            task A { init I1; exit e1; I1 --> e1; }
            """;
        var tree   = SyntaxTree.ParseText(source);
        var unit   = (CodeGenerationUnitSyntax) tree.Root;

        Assert.That(unit.LanguageVersionDirective, Is.Not.Null);
        Assert.That(unit.LanguageVersion.Value,    Is.EqualTo(2));
        Assert.That(unit.SyntaxTree.Diagnostics.Count(d => d.Descriptor.Id == "Nav3002"), Is.EqualTo(1));

        // Der überzählige Rest 'xy' ist ausgegraut; die '2' bleibt eine gefärbte Zahl.
        var tokens = DirectiveTokens(tree).ToList();
        Assert.That(tokens.Any(t => t.Classification == TextClassification.NumberLiteral), Is.True, "'2'");

        var xyStart = source.IndexOf("xy", System.StringComparison.Ordinal);
        var xy      = tokens.Single(t => t.Extent.Contains(TextExtent.FromBounds(xyStart, xyStart + 1)));
        Assert.That(xy.Classification, Is.EqualTo(TextClassification.Skiped));
    }

    [Test]
    public void UnknownPragma_ReportsNav3001() {

        // '#pragma' bleibt als Direktiv-Form erkannt, aber es gibt keine bekannten Pragmas mehr: jedes
        // Subjekt hinter 'pragma' (auch 'version') meldet Nav3001 und ist keine Versions-Direktive.
        var unit = Parse(
            """
            #pragma warning disable Nav1234
            task A { init I1; exit e1; I1 --> e1; }
            """);

        Assert.That(unit.LanguageVersionDirective, Is.Null);
        Assert.That(unit.LanguageVersion.Value,    Is.EqualTo(1));

        var ids = unit.SyntaxTree.Diagnostics.Select(d => d.Descriptor.Id).ToList();
        Assert.That(ids, Does.Contain("Nav3001"));
        Assert.That(ids, Does.Not.Contain("Nav3000"));
    }

    [Test]
    public void PragmaVersion_IsUnknownPragma_ReportsNav3001() {

        // 'version' ist kein Pragma-Subjekt mehr: '#pragma version 1' ist ein unbekanntes Pragma (Nav3001),
        // keine Versions-Direktive. Die Version bleibt Default.
        var unit = Parse(
            """
            #pragma version 1
            task A { init I1; exit e1; I1 --> e1; }
            """);

        Assert.That(unit.LanguageVersionDirective, Is.Null);
        Assert.That(unit.LanguageVersion.Value,    Is.EqualTo(1));
        Assert.That(unit.SyntaxTree.Directives().OfType<VersionDirectiveSyntax>().Count(), Is.EqualTo(0));

        var ids = unit.SyntaxTree.Diagnostics.Select(d => d.Descriptor.Id).ToList();
        Assert.That(ids, Does.Contain("Nav3001"));
    }

    [Test]
    public void PragmaWithoutSubject_ReportsNav3000() {

        // '#pragma' ohne Subjekt ist kein benennbares Pragma — es bleibt die generische unbekannte Direktive.
        var unit = Parse(
            """
            #pragma
            task A { init I1; exit e1; I1 --> e1; }
            """);

        var ids = unit.SyntaxTree.Diagnostics.Select(d => d.Descriptor.Id).ToList();
        Assert.That(ids, Does.Contain("Nav3000"));
        Assert.That(ids, Does.Not.Contain("Nav3001"));
    }

    [Test]
    public void UnknownDirective_ReportsNav3000() {

        // Ein unbekanntes Direktiv-Schlüsselwort (weder 'version' noch 'pragma') ist die generische
        // unbekannte Direktive (Nav3000).
        var unit = Parse(
            """
            #foo
            task A { init I1; exit e1; I1 --> e1; }
            """);

        var ids = unit.SyntaxTree.Diagnostics.Select(d => d.Descriptor.Id).ToList();
        Assert.That(ids, Does.Contain("Nav3000"));
        Assert.That(ids, Does.Not.Contain("Nav3001"));
    }

    [Test]
    public void MisplacedDirective_AfterMember_ReportsNav3003_AndDefaults() {

        var unit = Parse(
            """
            task A { init I1; exit e1; I1 --> e1; }
            #version 2
            """);

        // Nicht ganz oben ⇒ unwirksam (Default) und als Nav3003 gemeldet — nicht als Nav3000.
        Assert.That(unit.LanguageVersionDirective, Is.Null);
        Assert.That(unit.LanguageVersion.Value,    Is.EqualTo(1));

        var ids = unit.SyntaxTree.Diagnostics.Select(d => d.Descriptor.Id).ToList();
        Assert.That(ids, Does.Contain("Nav3003"));
        Assert.That(ids, Does.Not.Contain("Nav3000"));
        Assert.That(ids, Does.Not.Contain("Nav3002"));
    }

    [Test]
    public void DirectiveAfterOtherDirective_ReportsNav3003() {

        var unit = Parse(
            """
            #pragma warning disable Nav1
            #version 2
            task A { init I1; exit e1; I1 --> e1; }
            """);

        // Vor der Versions-Direktive steht eine andere Direktive (kein Trivia) ⇒ nicht ganz oben (Nav3003);
        // die vorangehende Direktive ist ein unbekanntes Pragma (Nav3001).
        Assert.That(unit.LanguageVersionDirective, Is.Null);
        Assert.That(unit.LanguageVersion.Value,    Is.EqualTo(1));

        var ids = unit.SyntaxTree.Diagnostics.Select(d => d.Descriptor.Id).ToList();
        Assert.That(ids, Does.Contain("Nav3003"));
        Assert.That(ids, Does.Contain("Nav3001"));
    }

    [Test]
    public void DuplicateDirective_ReportsNav3004_FirstWins() {

        var unit = Parse(
            """
            #version 2
            #version 3
            task A { init I1; exit e1; I1 --> e1; }
            """);

        // Die erste (wirksame) Versions-Direktive bestimmt die Version; die zweite ist ein Duplikat (Nav3004).
        // Beide sind eigenständige VersionDirectiveSyntax-Knoten — wirksam ist aber nur die erste.
        Assert.That(unit.LanguageVersionDirective, Is.Not.Null);
        Assert.That(unit.LanguageVersion.Value,    Is.EqualTo(2));
        Assert.That(unit.SyntaxTree.Directives().OfType<VersionDirectiveSyntax>().Count(), Is.EqualTo(2));

        var ids = unit.SyntaxTree.Diagnostics.Select(d => d.Descriptor.Id).ToList();
        Assert.That(ids, Does.Contain("Nav3004"));
        Assert.That(ids, Does.Not.Contain("Nav3000"));
        Assert.That(ids, Does.Not.Contain("Nav3003"));
    }

    [Test]
    public void DuplicateDirective_AfterMember_ReportsNav3003_NotNav3004() {

        var unit = Parse(
            """
            #version 2
            task A { init I1; exit e1; I1 --> e1; }
            #version 3
            """);

        // Die zweite Direktive steht hinter echtem Code: die Deplatzierung (Nav3003) ist das eigentliche
        // Problem und verdrängt die andernfalls nur lärmende Duplikat-Meldung (Nav3004). Beide sind
        // eigenständige VersionDirectiveSyntax-Knoten; wirksam bleibt die erste (wohlplatzierte).
        Assert.That(unit.LanguageVersionDirective, Is.Not.Null);
        Assert.That(unit.LanguageVersion.Value,    Is.EqualTo(2));
        Assert.That(unit.SyntaxTree.Directives().OfType<VersionDirectiveSyntax>().Count(), Is.EqualTo(2));

        var ids = unit.SyntaxTree.Diagnostics.Select(d => d.Descriptor.Id).ToList();
        Assert.That(ids, Does.Contain("Nav3003"));
        Assert.That(ids, Does.Not.Contain("Nav3004"));
    }

    [Test]
    public void DirectiveDiagnostic_SpansWholeDirective_NotJustTheHash() {

        // Die Squiggle soll die ganze Direktive markieren (# … Versionswert), nicht nur das '#'.
        var source = """
            task A { init I1; exit e1; I1 --> e1; }
            #version 2
            """;
        var unit   = Parse(source);

        var diagnostic = unit.SyntaxTree.Diagnostics.Single(d => d.Descriptor.Id == "Nav3003");

        var start = source.IndexOf("#version", System.StringComparison.Ordinal);
        Assert.That(diagnostic.Location.Start,  Is.EqualTo(start));
        Assert.That(diagnostic.Location.Length, Is.EqualTo("#version 2".Length));

        // Auch die Zeilen-Range muss die volle Breite tragen (nicht nullbreit) — sonst zieht VS Code die
        // Squiggle nur über ein Zeichen, obwohl der Extent stimmt.
        Assert.That(diagnostic.Location.StartLine, Is.EqualTo(diagnostic.Location.EndLine));
        Assert.That(diagnostic.Location.EndCharacter - diagnostic.Location.StartCharacter,
                    Is.EqualTo("#version 2".Length));
    }

    [Test]
    public void MidLineHash_AfterCode_IsNotADirective_ReportsNav0000() {

        // Ein '#' mitten in der Zeile (hinter Code) beginnt keine Direktive: der Lexer erzeugt dafür ein
        // einzelnes unbekanntes Zeichen (Nav0000), der Zeilenrest lext gewöhnlich weiter. Es entsteht daher
        // keine zweite Versions-Direktive und kein Nav3003/Nav3004 — nur die erste, wohlplatzierte Direktive
        // bleibt wirksam.
        var source = """
            #version 1
            task A { init I1; exit e1; I1 --> e1; } #version 1
            """;
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
    public void DuplicateDirective_BothAtTop_ReportsNav3004_SpanningWholeDirective() {

        // Zwei Versions-Direktiven, beide am Kopf (jede am Zeilenanfang, nur Trivia dazwischen): die zweite
        // ist ein echtes Duplikat (Nav3004). Die Squiggle soll die ganze zweite Direktive markieren
        // (# … Versionswert), nicht nur das '#'.
        var source = """
            #version 1
            #version 1
            task A { init I1; exit e1; I1 --> e1; }
            """;
        var unit   = Parse(source);

        var diagnostic = unit.SyntaxTree.Diagnostics.Single(d => d.Descriptor.Id == "Nav3004");
        var start      = source.LastIndexOf("#version", System.StringComparison.Ordinal);

        Assert.That(diagnostic.Location.Start,  Is.EqualTo(start));
        Assert.That(diagnostic.Location.Length, Is.EqualTo("#version 1".Length));

        // Auch die Zeilen-Range muss die volle Breite tragen (nicht nullbreit).
        Assert.That(diagnostic.Location.StartLine, Is.EqualTo(diagnostic.Location.EndLine));
        Assert.That(diagnostic.Location.EndCharacter - diagnostic.Location.StartCharacter,
                    Is.EqualTo("#version 1".Length));
    }

    // Die Token einer Direktive liegen nicht mehr im flachen Strom, sondern lokal am Direktiv-Knoten
    // (strukturierte Trivia) — erreichbar über die Trivia.
    static System.Collections.Generic.IEnumerable<SyntaxToken> DirectiveTokens(SyntaxTree tree) {
        return tree.Directives().SelectMany(directive => directive.ChildTokens());
    }

    [Test]
    public void MisplacedDirective_StillClassifiesNumberValue() {

        // Auch eine deplatzierte Versions-Direktive (Nav3003) behält die Färbung ihrer Rumpf-Token: der Wert
        // wird weiterhin als Zahl klassifiziert, obwohl die Direktive unwirksam ist.
        var tree = SyntaxTree.ParseText(
            """
            task A { init I1; exit e1; I1 --> e1; }
            #version 7
            """);

        Assert.That(DirectiveTokens(tree).Any(t => t.Classification == TextClassification.NumberLiteral), Is.True);
    }

    [Test]
    public void WellFormedDirective_ClassifiesVersionKeywordAndNumber() {

        // Positionen in "#version 12": '#'=0, 'version'=1..7, ' '=8, '12'=9..10.
        var tree   = SyntaxTree.ParseText(
            """
            #version 12
            task A { init I1; exit e1; I1 --> e1; }
            """);
        var tokens = DirectiveTokens(tree).ToList();

        TextClassification At(int position) {
            return tokens.Single(t => t.Extent.Contains(TextExtent.FromBounds(position, position + 1))).Classification;
        }

        Assert.That(At(0),  Is.EqualTo(TextClassification.PreprocessorKeyword), "'#'");
        Assert.That(At(1),  Is.EqualTo(TextClassification.PreprocessorKeyword), "Anfang von 'version'");
        Assert.That(At(7),  Is.EqualTo(TextClassification.PreprocessorKeyword), "Ende von 'version'");
        Assert.That(At(9),  Is.EqualTo(TextClassification.NumberLiteral),       "erste Ziffer von '12'");
        Assert.That(At(10), Is.EqualTo(TextClassification.NumberLiteral),       "zweite Ziffer von '12'");
    }

    [Test]
    public void InvalidVersion_DoesNotClassifyAsNumber() {

        // 'version' bleibt Präprozessor-Schlüsselwort, aber der ungültige Wert wird nicht als Zahl gefärbt.
        var tree = SyntaxTree.ParseText(
            """
            #version abc
            task A { init I1; exit e1; I1 --> e1; }
            """);

        Assert.That(DirectiveTokens(tree).Any(t => t.Classification == TextClassification.NumberLiteral), Is.False);
    }

    [Test]
    public void WellFormedDirective_RoundTrips() {

        var source = """
            #version 2
            task A { init I1; exit e1; I1 --> e1; }
            """;
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

    // -- Zentrale Versions-Autorität + semantische Nav5001-Prüfung --------------------------------------------

    static CodeGenerationUnit BuildUnit(string text) {
        var syntax = Syntax.ParseCodeGenerationUnit(text);
        return CodeGenerationUnit.FromCodeGenerationUnitSyntax(syntax);
    }

    [Test]
    public void SupportedVersions_IsTheSingleAuthority() {

        // Default und Latest werden aus der zentralen Menge abgeleitet — keine Magic-Values im Code.
        Assert.That(NavLanguageVersion.SupportedVersions, Does.Contain(NavLanguageVersion.Version1));
        Assert.That(NavLanguageVersion.Default, Is.EqualTo(NavLanguageVersion.Version1));
        Assert.That(NavLanguageVersion.Latest,  Is.EqualTo(NavLanguageVersion.SupportedVersions.Last()));

        Assert.That(NavLanguageVersion.Version1.IsSupported,      Is.True);
        Assert.That(new NavLanguageVersion(99).IsSupported,       Is.False);
        Assert.That(new NavLanguageVersion(0).IsSupported,        Is.False);
    }

    [Test]
    public void SupportedVersion_ReportsNoNav5001() {

        var unit = BuildUnit("#version 1\r\ntask A { init I1; exit e1; I1 --> e1; }");

        Assert.That(unit.LanguageVersion.Value, Is.EqualTo(1));
        Assert.That(unit.Diagnostics.Select(d => d.Descriptor.Id), Does.Not.Contain("Nav5001"));
    }

    [Test]
    public void UnsupportedVersion_ReportsNav5001_Semantically() {

        // Version 99 ist syntaktisch wohlgeformt (kein Nav3002), aber der Engine unbekannt: das ist eine
        // rein semantische Nav5001 (im CodeGenerationUnit), kein Syntaxfehler.
        var unit = BuildUnit("#version 99\r\ntask A { init I1; exit e1; I1 --> e1; }");

        Assert.That(unit.Diagnostics.Count(d => d.Descriptor.Id == "Nav5001"), Is.EqualTo(1));
        Assert.That(unit.Syntax.SyntaxTree.Diagnostics.Select(d => d.Descriptor.Id), Does.Not.Contain("Nav3002"));
    }

    [Test]
    public void UnsupportedVersion_Nav5001_SpansWholeDirective() {

        var source = """
            #version 99
            task A { init I1; exit e1; I1 --> e1; }
            """;
        var unit   = BuildUnit(source);

        var diagnostic = unit.Diagnostics.Single(d => d.Descriptor.Id == "Nav5001");
        Assert.That(diagnostic.Location.Start,  Is.EqualTo(source.IndexOf("#version", System.StringComparison.Ordinal)));
        Assert.That(diagnostic.Location.Length, Is.EqualTo("#version 99".Length));
    }

    [Test]
    public void UnsupportedVersion_MisplacedDirective_OnlyNav3003_NoNav5001() {

        // Eine deplatzierte (unwirksame) Versions-Direktive ist bereits per Nav3003 gemeldet; die
        // Versionsgültigkeit prüft nur die wirksame Direktive, daher kommt hier kein Nav5001 hinzu.
        var unit = BuildUnit("task A { init I1; exit e1; I1 --> e1; }\r\n#version 99");

        var ids = unit.Diagnostics.Select(d => d.Descriptor.Id)
                      .Concat(unit.Syntax.SyntaxTree.Diagnostics.Select(d => d.Descriptor.Id))
                      .ToList();
        Assert.That(ids, Does.Contain("Nav3003"));
        Assert.That(ids, Does.Not.Contain("Nav5001"));
    }

}
