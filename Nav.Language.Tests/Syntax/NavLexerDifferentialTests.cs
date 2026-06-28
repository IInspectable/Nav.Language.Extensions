#region Using Directives

using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Nav.Language.Tests;

/// <summary>
/// Differentielles Sicherheitsnetz für den handgeschriebenen <see cref="NavLexer"/>: seine Token-Folge
/// muss <b>Token für Token</b> (Start, Länge, lexikalischer Typ) mit dem beobachtbaren Token-Strom der
/// bisherigen ANTLR-Pipeline (<see cref="SyntaxTree.ParseText(string, string, System.Threading.CancellationToken)"/>
/// → <see cref="SyntaxTree.Tokens"/>) übereinstimmen.
/// <para/>
/// Verglichen wird bewusst gegen den <i>nachbereiteten</i> Strom (inkl. SingleLineComment-/EOL-Split und
/// Präprozessor-Aufteilung), nicht gegen ANTLRs rohen <c>cts.AllTokens</c> — genau diesen Strom
/// konsumieren alle Hosts, und genau ihn erzeugt der neue Lexer in einem Durchlauf. Die
/// kontextabhängige <see cref="TextClassification"/> bleibt außen vor: die vergibt erst der Parser,
/// der Lexer kennt nur lexikalische Typen.
/// </summary>
[TestFixture]
public class NavLexerDifferentialTests {

    [Test, TestCaseSource(nameof(CorpusFiles))]
    public void LexerMatchesReferenceTokenStream(string navFile) {

        var source = File.ReadAllText(navFile);

        Assert.That(DumpLexer(NavLexer.Lex(source)), Is.EqualTo(DumpReference(source)),
                    $"Der handgeschriebene Lexer weicht für '{Path.GetFileName(navFile)}' vom Referenz-Token-Strom ab.");
    }

    [Test, TestCaseSource(nameof(CorpusFiles))]
    public void LexerRoundTripsCorpus(string navFile) {

        var source = File.ReadAllText(navFile);

        Assert.That(RoundTrip(NavLexer.Lex(source), source), Is.EqualTo(source),
                    $"Der Lexer-Token-Strom reproduziert den Quelltext von '{Path.GetFileName(navFile)}' nicht lückenlos.");
    }

    /// <summary>
    /// Inkrementelles Tippen: für jedes Präfix jeder Korpus-Datei muss der Lexer exakt den
    /// Referenz-Strom liefern. Deckt zahllose entartete Zwischenzustände gratis ab (halbe Token,
    /// offene Strings/Kommentare, abgeschnittene Mehrzeichen-Kanten).
    /// </summary>
    [Test, TestCaseSource(nameof(CorpusFiles))]
    public void LexerMatchesReferenceForAllTypingPrefixes(string navFile) {

        var source = File.ReadAllText(navFile);

        for (var length = 0; length <= source.Length; length++) {
            var prefix = source.Substring(0, length);

            Assert.That(DumpLexer(NavLexer.Lex(prefix)), Is.EqualTo(DumpReference(prefix)),
                        $"Abweichung beim Präfix der Länge {length} von '{Path.GetFileName(navFile)}'.");
        }
    }

    #region Infrastructure

    // Eine Zeile je Token: Start, Länge, lexikalischer Typ. Klassifikation/Parent bewusst weggelassen.
    static string DumpLexer(ImmutableArray<RawToken> tokens) {
        return string.Join("\n", tokens.Select(token => Format(token.Start, token.Length, token.Type)));
    }

    static string DumpReference(string source) {
        var tree = SyntaxTree.ParseText(source);
        return string.Join("\n", tree.Tokens.Select(token => Format(token.Start, token.Length, token.Type)));
    }

    static string Format(int start, int length, SyntaxTokenType type) {
        return $"{start,5} {length,4}  {type}";
    }

    static string RoundTrip(ImmutableArray<RawToken> tokens, string source) {
        var sb = new StringBuilder();
        foreach (var token in tokens) {
            sb.Append(source.Substring(token.Start, token.Length));
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