#region Using Directives

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Nav.Language.Tests;

/// <summary>
/// Golden-Master der Syntax-Schicht je Korpus-Datei. Friert den exakt beobachtbaren Output des
/// <i>heutigen</i> (ANTLR-basierten) Parsers ein, damit der künftige handgeschriebene Parser
/// Token für Token, Knoten für Knoten <i>und</i> Diagnose für Diagnose dagegen diffbar ist.
///
/// Drei Golden-Stränge:
/// <list type="bullet">
///   <item><description><c>.tokens</c> — der vollständige Token-Strom (inkl. Trivia), plus
///   Full-Fidelity-Round-Trip.</description></item>
///   <item><description><c>.tree</c> — die Baumstruktur (Verschachtelung, Extents, angehängte
///   Token), plus reine Struktur-Invarianten über den ganzen Korpus (Parent-Zuordnung,
///   Kind-in-Parent-Extent, überlappungsfreie Geschwister).</description></item>
///   <item><description><c>.diag</c> — die reinen <i>Syntax</i>-Diagnostics (direkt aus
///   <see cref="SyntaxTree.Diagnostics"/>, ohne das semantische Modell). Nagelt das
///   Error-Recovery-Verhalten fest — der Bereich, in dem der neue Parser am ehesten abweicht.
///   Eine leere Golden-Datei pinnt „diese Datei erzeugt keine Syntaxfehler".</description></item>
/// </list>
/// </summary>
[TestFixture]
public class SyntaxGoldenTests {

    const string GoldenExtension = ".tokens";
    const string TreeExtension   = ".tree";
    const string DiagExtension   = ".diag";

    [Test, TestCaseSource(nameof(GetCorpusFiles))]
    public void TokenStreamMatchesGolden(CorpusFile corpus) {

        var tree   = ParseCorpusFile(corpus, out _);
        var actual = DumpTokens(tree);

        var goldenPath = corpus.FilePath + GoldenExtension;

        Assert.That(File.Exists(goldenPath), Is.True,
                    $"Golden-Datei '{goldenPath}' fehlt. Den [Explicit]-Test '{nameof(UpdateGolden)}' ausführen, um sie zu erzeugen.");

        var expected = File.ReadAllText(goldenPath);

        Assert.That(Normalize(actual), Is.EqualTo(Normalize(expected)),
                    $"Token-Stream von '{corpus}' weicht vom Golden '{Path.GetFileName(goldenPath)}' ab.");
    }

    [Test, TestCaseSource(nameof(GetCorpusFiles))]
    public void TokenStreamRoundTrips(CorpusFile corpus) {

        var tree = ParseCorpusFile(corpus, out var source);

        Assert.That(RoundTrip(tree), Is.EqualTo(source),
                    $"Round-Trip von '{corpus}' reproduziert den Quelltext nicht lückenlos.");
    }

    /// <summary>
    /// Robustheit gegen unvollständige Eingaben: jedes Tipp-Präfix jeder Korpus-Datei muss sich ohne
    /// Ausnahme parsen lassen und lückenlos round-trippen. Genau das stresst die Recovery — jeder
    /// Zwischenstand beim Tippen ist eine teilweise wohlgeformte Eingabe.
    /// </summary>
    [Test, TestCaseSource(nameof(GetCorpusFiles))]
    public void ParsesAndRoundTripsAllTypingPrefixes(CorpusFile corpus) {

        var source = File.ReadAllText(corpus.FilePath);

        for (var length = 0; length <= source.Length; length++) {
            var prefix = source.Substring(0, length);

            var tree = SyntaxTree.ParseText(prefix, corpus.FilePath);

            Assert.That(RoundTrip(tree), Is.EqualTo(prefix),
                        $"Round-Trip beim Präfix der Länge {length} von '{corpus}' reproduziert den Quelltext nicht lückenlos.");
        }
    }

    [Test, TestCaseSource(nameof(GetCorpusFiles))]
    public void SyntaxDiagnosticsMatchGolden(CorpusFile corpus) {

        var tree   = ParseCorpusFile(corpus, out _);
        var actual = DumpDiagnostics(tree);

        var goldenPath = corpus.FilePath + DiagExtension;

        Assert.That(File.Exists(goldenPath), Is.True,
                    $"Golden-Datei '{goldenPath}' fehlt. Den [Explicit]-Test '{nameof(UpdateGolden)}' ausführen, um sie zu erzeugen.");

        var expected = File.ReadAllText(goldenPath);

        Assert.That(Normalize(actual), Is.EqualTo(Normalize(expected)),
                    $"Syntax-Diagnostics von '{corpus}' weichen vom Golden '{Path.GetFileName(goldenPath)}' ab.");
    }

    [Test, TestCaseSource(nameof(GetCorpusFiles))]
    public void NodeTreeMatchesGolden(CorpusFile corpus) {

        var tree   = ParseCorpusFile(corpus, out _);
        var actual = DumpTree(tree.Root);

        var goldenPath = corpus.FilePath + TreeExtension;

        Assert.That(File.Exists(goldenPath), Is.True,
                    $"Golden-Datei '{goldenPath}' fehlt. Den [Explicit]-Test '{nameof(UpdateGolden)}' ausführen, um sie zu erzeugen.");

        var expected = File.ReadAllText(goldenPath);

        Assert.That(Normalize(actual), Is.EqualTo(Normalize(expected)),
                    $"Knotenbaum von '{corpus}' weicht vom Golden '{Path.GetFileName(goldenPath)}' ab.");
    }

    [Test, TestCaseSource(nameof(GetCorpusFiles))]
    public void EveryNodeAndTokenIsParented(CorpusFile corpus) {

        var tree = ParseCorpusFile(corpus, out _);

        Assert.That(tree.Root.Parent, Is.Null, "Der Wurzelknoten darf keinen Parent besitzen.");

        foreach (var node in tree.Root.DescendantNodes()) {
            Assert.That(node.Parent, Is.Not.Null,
                        $"Knoten {Describe(node)} besitzt keinen Parent.");
        }

        foreach (var token in tree.Tokens) {
            Assert.That(token.Parent, Is.Not.Null,
                        $"Token {token.Type} {token.Extent} besitzt keinen Parent.");
        }
    }

    [Test, TestCaseSource(nameof(GetCorpusFiles))]
    public void ChildExtentsLieWithinParentExtent(CorpusFile corpus) {

        var tree = ParseCorpusFile(corpus, out _);

        foreach (var node in tree.Root.DescendantNodesAndSelf()) {
            if (node.Extent.IsMissing) {
                continue;
            }

            foreach (var child in node.ChildNodes()) {
                if (child.Extent.IsMissing) {
                    continue;
                }

                Assert.That(node.Extent.Contains(child.Extent), Is.True,
                            $"Kindknoten {Describe(child)} liegt nicht im Extent des Parents {Describe(node)}.");
            }
        }
    }

    [Test, TestCaseSource(nameof(GetCorpusFiles))]
    public void SiblingExtentsDoNotOverlap(CorpusFile corpus) {

        var tree = ParseCorpusFile(corpus, out _);

        foreach (var node in tree.Root.DescendantNodesAndSelf()) {

            var siblings = node.ChildNodes()
                               .Where(child => !child.Extent.IsMissing)
                               .OrderBy(child => child.Extent.Start)
                               .ThenBy(child => child.Extent.End)
                               .ToList();

            for (var i = 1; i < siblings.Count; i++) {
                Assert.That(siblings[i - 1].Extent.End, Is.LessThanOrEqualTo(siblings[i].Extent.Start),
                            $"Geschwister-Extents überlappen: {Describe(siblings[i - 1])} und {Describe(siblings[i])} unter {Describe(node)}.");
            }
        }
    }

    /// <summary>
    /// Schreibt alle <c>.tokens</c>-, <c>.tree</c>- und <c>.diag</c>-Golden neu
    /// (Muster: <see cref="RegressionTests.GenerateFiles"/>).
    /// </summary>
    [Test, Explicit]
    public void UpdateGolden() {

        var utf8Bom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

        foreach (var corpus in GetCorpusFiles()) {
            var tree = ParseCorpusFile(corpus, out _);
            File.WriteAllText(corpus.FilePath + GoldenExtension, DumpTokens(tree),      utf8Bom);
            File.WriteAllText(corpus.FilePath + TreeExtension,   DumpTree(tree.Root),   utf8Bom);
            File.WriteAllText(corpus.FilePath + DiagExtension,   DumpDiagnostics(tree), utf8Bom);
        }
    }

    #region Infrastructure

    static SyntaxTree ParseCorpusFile(CorpusFile corpus, out string source) {
        source = File.ReadAllText(corpus.FilePath);
        return SyntaxTree.ParseText(source, corpus.FilePath);
    }

    /// <summary>
    /// Serialisiert den kompletten Token-Strom (inkl. Trivia) — eine Zeile je Token mit Start, Länge,
    /// Typ, Klassifikation, IsMissing und dem Knotentyp des Parents. Die Liste ist bereits stabil
    /// nach Position sortiert.
    /// </summary>
    static string DumpTokens(SyntaxTree tree) {

        var sb = new StringBuilder();
        foreach (var token in tree.Tokens) {
            sb.Append(token.Start.ToString().PadLeft(5));
            sb.Append(' ');
            sb.Append(token.Length.ToString().PadLeft(4));
            sb.Append("  ");
            sb.Append(token.Type.ToString().PadRight(24));
            sb.Append(token.Classification.ToString().PadRight(20));
            sb.Append(token.IsMissing ? "missing  " : "         ");
            sb.Append(token.Parent?.GetType().Name ?? "<null>");
            sb.Append('\n');
        }

        return sb.ToString();
    }

    /// <summary>
    /// Serialisiert den Knotenbaum — je Knoten eine eingerückte Zeile mit <c>GetType().Name</c>, dem
    /// <see cref="TextExtent"/> und (sofern vorhanden) den Typen der direkt angehängten Token. Die
    /// Kindknoten kommen über <see cref="SyntaxNode.ChildNodes"/> in Quelltext-Reihenfolge.
    /// </summary>
    static string DumpTree(SyntaxNode root) {
        var sb = new StringBuilder();
        DumpNode(root, depth: 0, sb);
        return sb.ToString();
    }

    static void DumpNode(SyntaxNode node, int depth, StringBuilder sb) {

        sb.Append(' ', depth * 2);
        sb.Append(node.GetType().Name);
        sb.Append(' ');
        sb.Append(node.Extent.ToString());

        if (!node.Extent.IsMissing) {
            var childTokens = node.ChildTokens().ToList();
            if (childTokens.Count > 0) {
                sb.Append("  {");
                sb.Append(string.Join(", ", childTokens.Select(token => token.Type.ToString())));
                sb.Append('}');
            }
        }

        sb.Append('\n');

        foreach (var child in node.ChildNodes()) {
            DumpNode(child, depth + 1, sb);
        }
    }

    static string Describe(SyntaxNode node) {
        return $"{node.GetType().Name} {node.Extent}";
    }

    /// <summary>
    /// Serialisiert die reinen Syntax-Diagnostics — eine Zeile je Diagnose im
    /// <see cref="UnitTestDiagnosticFormatter"/>-Format (<c>//==>>[Kategorie](Zeile,Spalte,…): …</c>,
    /// dasselbe wie <see cref="DiagnosticTests"/>). Stabile Reihenfolge: erst Error/Warning/Suggestion,
    /// innerhalb dessen nach Position. Kein Treffer ⇒ leerer String.
    /// </summary>
    static string DumpDiagnostics(SyntaxTree tree) {

        var byPosition = tree.Diagnostics
                             .SelectMany(diagnostic => diagnostic.ExpandLocations())
                             .OrderBy(diagnostic => diagnostic.Location.Start)
                             .ToList();

        var ordered = byPosition.Errors()
                                .Concat(byPosition.Warnings())
                                .Concat(byPosition.Suggestions());

        var sb = new StringBuilder();
        foreach (var diagnostic in ordered) {
            sb.Append(diagnostic.ToString(UnitTestDiagnosticFormatter.Instance));
            sb.Append('\n');
        }

        return sb.ToString();
    }

    static string RoundTrip(SyntaxTree tree) {
        var sb = new StringBuilder();
        foreach (var token in tree.Tokens) {
            sb.Append(token.ToString());
        }

        return sb.ToString();
    }

    static string Normalize(string text) {
        return text.Replace("\r\n", "\n").Replace("\r", "\n");
    }

    public static IEnumerable<CorpusFile> GetCorpusFiles() {

        var directory = TestDataDirectory.Resolve(@"Syntax\Tests");

        foreach (var navFile in Directory.EnumerateFiles(directory, "*.nav", SearchOption.TopDirectoryOnly)) {
            yield return new CorpusFile { FilePath = navFile };
        }
    }

    public class CorpusFile {

        public string FilePath { get; set; }

        public override string ToString() {
            return FilePath == null ? "Unknown" : Path.GetFileName(FilePath);
        }
    }

    #endregion

}
