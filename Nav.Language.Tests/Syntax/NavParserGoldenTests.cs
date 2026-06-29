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
/// Golden-Master der Syntax-Schicht je Korpus-Datei — diesmal vom handgeschriebenen
/// <see cref="NavParser"/> erzeugt (eigener Golden-Satz <c>*.hand.tokens</c>/<c>*.hand.tree</c>/
/// <c>*.hand.diag</c>, parallel zu den ANTLR-Golden aus <see cref="SyntaxGoldenTests"/>).
/// <para/>
/// Hintergrund: Sobald der Handparser fehlertolerante Recovery betreibt, weicht er bei nicht
/// wohlgeformten Eingaben <b>bewusst</b> von ANTLR ab (bessere Meldungen, andere Missing-/Skipped-Token).
/// Das Differential-Gate gegen ANTLR (<see cref="NavParserDifferentialTests"/>) stellt solche Dateien
/// daher zurück. Dieser zweite Golden-Satz nagelt den Output des Handparsers <i>für sich</i> fest:
/// Token, Baum und Diagnostics je Korpus-Datei werden gepinnt, sodass jede Recovery-Änderung als
/// reviewbarer Diff erscheint — auch dort, wo ANTLR keine 1:1-Referenz mehr ist.
/// </summary>
[TestFixture]
public class NavParserGoldenTests {

    const string TokensExtension = ".hand.tokens";
    const string TreeExtension   = ".hand.tree";
    const string DiagExtension   = ".hand.diag";

    [Test, TestCaseSource(nameof(GetCorpusFiles))]
    public void TokenStreamMatchesGolden(CorpusFile corpus) {

        var tree   = ParseCorpusFile(corpus, out _);
        var actual = DumpTokens(tree);

        var goldenPath = corpus.FilePath + TokensExtension;

        Assert.That(File.Exists(goldenPath), Is.True,
                    $"Golden-Datei '{goldenPath}' fehlt. Den [Explicit]-Test '{nameof(UpdateGolden)}' ausführen, um sie zu erzeugen.");

        var expected = File.ReadAllText(goldenPath);

        Assert.That(Normalize(actual), Is.EqualTo(Normalize(expected)),
                    $"Token-Stream des Handparsers für '{corpus}' weicht vom Golden '{Path.GetFileName(goldenPath)}' ab.");
    }

    [Test, TestCaseSource(nameof(GetCorpusFiles))]
    public void TokenStreamRoundTrips(CorpusFile corpus) {

        var tree = ParseCorpusFile(corpus, out var source);

        Assert.That(RoundTrip(tree), Is.EqualTo(source),
                    $"Round-Trip des Handparsers für '{corpus}' reproduziert den Quelltext nicht lückenlos.");
    }

    /// <summary>
    /// Robustheit gegen unvollständige Eingaben: jedes Tipp-Präfix jeder Korpus-Datei muss sich ohne
    /// Ausnahme parsen lassen und lückenlos round-trippen. Genau das stresst die Recovery — jeder
    /// Zwischenstand beim Tippen ist eine teilweise wohlgeformte Eingabe. (Ein Golden-Vergleich gegen
    /// ANTLR ist hier nicht möglich, weil der Handparser bei Recovery bewusst abweicht.)
    /// </summary>
    [Test, TestCaseSource(nameof(GetCorpusFiles))]
    public void ParsesAndRoundTripsAllTypingPrefixes(CorpusFile corpus) {

        var source = File.ReadAllText(corpus.FilePath);

        for (var length = 0; length <= source.Length; length++) {
            var prefix = source.Substring(0, length);

            var tree = NavParser.Parse(prefix, corpus.FilePath);

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
                    $"Syntax-Diagnostics des Handparsers für '{corpus}' weichen vom Golden '{Path.GetFileName(goldenPath)}' ab.");
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
                    $"Knotenbaum des Handparsers für '{corpus}' weicht vom Golden '{Path.GetFileName(goldenPath)}' ab.");
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
    /// Schreibt alle <c>.hand.tokens</c>-, <c>.hand.tree</c>- und <c>.hand.diag</c>-Golden neu
    /// (Muster: <see cref="SyntaxGoldenTests.UpdateGolden"/>).
    /// </summary>
    [Test, Explicit]
    public void UpdateGolden() {

        var utf8Bom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

        foreach (var corpus in GetCorpusFiles()) {
            var tree = ParseCorpusFile(corpus, out _);
            File.WriteAllText(corpus.FilePath + TokensExtension, DumpTokens(tree),      utf8Bom);
            File.WriteAllText(corpus.FilePath + TreeExtension,   DumpTree(tree.Root),   utf8Bom);
            File.WriteAllText(corpus.FilePath + DiagExtension,   DumpDiagnostics(tree), utf8Bom);
        }
    }

    #region Infrastructure

    static SyntaxTree ParseCorpusFile(CorpusFile corpus, out string source) {
        source = File.ReadAllText(corpus.FilePath);
        return NavParser.Parse(source, corpus.FilePath);
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
    /// dasselbe wie <see cref="SyntaxGoldenTests"/>). Stabile Reihenfolge: erst Error/Warning/Suggestion,
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
