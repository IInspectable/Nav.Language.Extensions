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
/// Differentielles Sicherheitsnetz für den handgeschriebenen <see cref="NavParser"/>: für jede
/// <b>wohlgeformte</b> Korpus-Datei muss er denselben Knotenbaum und denselben Token-Strom liefern wie die
/// bisherige ANTLR-Pipeline (<see cref="SyntaxTree.ParseText(string, string, System.Threading.CancellationToken)"/>).
/// Verglichen wird Knoten für Knoten (Typ, Extent, angehängte Token) und Token für Token (Start, Länge, Typ,
/// Klassifikation, Parent-Knotentyp), dazu der Full-Fidelity-Round-Trip.
/// <para/>
/// Bewusst nur der wohlgeformte Fall: Dateien, für die ANTLR Diagnostics meldet (Error-Recovery), werden
/// übersprungen — fehlertolerante Recovery samt Diagnostics-Parität ist Gegenstand eines eigenen Schritts.
/// </summary>
[TestFixture]
public class NavParserDifferentialTests {

    [Test, TestCaseSource(nameof(CorpusFiles))]
    public void NodeTreeMatchesReference(string navFile) {

        var source = File.ReadAllText(navFile);
        var reference = SyntaxTree.ParseText(source, navFile);
        SkipIfNotWellFormed(reference, navFile);

        var actual = NavParser.Parse(source, navFile);

        Assert.That(DumpTree(actual.Root), Is.EqualTo(DumpTree(reference.Root)),
                    $"Der handgeschriebene Parser liefert für '{Path.GetFileName(navFile)}' einen anderen Knotenbaum als ANTLR.");
    }

    [Test, TestCaseSource(nameof(CorpusFiles))]
    public void TokenStreamMatchesReference(string navFile) {

        var source = File.ReadAllText(navFile);
        var reference = SyntaxTree.ParseText(source, navFile);
        SkipIfNotWellFormed(reference, navFile);

        var actual = NavParser.Parse(source, navFile);

        Assert.That(DumpTokens(actual), Is.EqualTo(DumpTokens(reference)),
                    $"Der handgeschriebene Parser liefert für '{Path.GetFileName(navFile)}' einen anderen Token-Strom als ANTLR.");
    }

    [Test, TestCaseSource(nameof(CorpusFiles))]
    public void RoundTripsCorpus(string navFile) {

        var source = File.ReadAllText(navFile);
        var reference = SyntaxTree.ParseText(source, navFile);
        SkipIfNotWellFormed(reference, navFile);

        var actual = NavParser.Parse(source, navFile);

        Assert.That(RoundTrip(actual), Is.EqualTo(source),
                    $"Der Token-Strom des handgeschriebenen Parsers reproduziert den Quelltext von '{Path.GetFileName(navFile)}' nicht lückenlos.");
    }

    #region Infrastructure

    static void SkipIfNotWellFormed(SyntaxTree reference, string navFile) {
        if (!reference.Diagnostics.IsEmpty) {
            Assert.Ignore($"'{Path.GetFileName(navFile)}' ist nicht wohlgeformt (ANTLR meldet Diagnostics) — Recovery folgt in einem eigenen Schritt.");
        }
    }

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

    static string RoundTrip(SyntaxTree tree) {
        var sb = new StringBuilder();
        foreach (var token in tree.Tokens) {
            sb.Append(token.ToString());
        }

        return sb.ToString();
    }

    public static IEnumerable<TestCaseData> CorpusFiles() {

        var directory = TestDataDirectory.Resolve(@"Syntax\Tests");

        foreach (var navFile in Directory.EnumerateFiles(directory, "*.nav", SearchOption.TopDirectoryOnly)) {
            yield return new TestCaseData(navFile).SetName(Path.GetFileName(navFile));
        }
    }

    #endregion

}
