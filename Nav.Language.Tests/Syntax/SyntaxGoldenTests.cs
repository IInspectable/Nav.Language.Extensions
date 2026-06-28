#region Using Directives

using System.Collections.Generic;
using System.IO;
using System.Text;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;

#endregion

namespace Nav.Language.Tests;

/// <summary>
/// Golden-Master des vollständigen Token-Stroms je Korpus-Datei plus Full-Fidelity-Round-Trip.
/// Friert den exakt beobachtbaren Output des <i>heutigen</i> (ANTLR-basierten) Parsers ein, damit
/// der künftige handgeschriebene Parser Token für Token dagegen diffbar ist
/// (siehe <c>doc\nav-parser-test-plan.md</c>, Step 1).
/// </summary>
[TestFixture]
public class SyntaxGoldenTests {

    const string GoldenExtension = ".tokens";

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
    /// Schreibt alle <c>.tokens</c>-Golden neu (Muster: <see cref="RegressionTests.GenerateFiles"/>).
    /// </summary>
    [Test, Explicit]
    public void UpdateGolden() {

        var utf8Bom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

        foreach (var corpus in GetCorpusFiles()) {
            var tree = ParseCorpusFile(corpus, out _);
            var dump = DumpTokens(tree);
            File.WriteAllText(corpus.FilePath + GoldenExtension, dump, utf8Bom);
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
