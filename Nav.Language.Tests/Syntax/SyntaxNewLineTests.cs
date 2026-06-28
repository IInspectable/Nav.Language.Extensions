#region Using Directives

using System.Collections.Generic;
using System.Linq;
using System.Text;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;

#endregion

namespace Nav.Language.Tests;

/// <summary>
/// Pinnt das Zeilenende-Verhalten des heutigen Lexers (<c>\n</c>, <c>\r</c>, <c>\r\n</c> sowie die
/// exotischen NEL/LS/PS (U+0085/U+2028/U+2029) - der mit Abstand fragilste Bereich für den
/// künftigen handgeschriebenen Parser („das leidige NL-Problem").
/// <para/>
/// Bewusst <b>Inline-Tests, kein Datei-Korpus</b>: Die NL-Sequenzen werden als C#-Escapes bzw. über
/// numerische <c>(char)</c>-Casts gebildet und stehen NIE als literale Zeilenenden in dieser Datei.
/// Damit ist die Testeingabe immun gegen jede EOL-Normalisierung durch git (<c>.gitattributes</c>
/// erzwingt sonst CRLF) oder Editoren — ein <c>.nav</c> mit reinem <c>\n</c> bliebe das nämlich nicht.
/// </summary>
[TestFixture]
public class SyntaxNewLineTests {

    static readonly string Nel = ((char) 0x0085).ToString(); // NEXT LINE
    static readonly string Ls  = ((char) 0x2028).ToString(); // LINE SEPARATOR
    static readonly string Ps  = ((char) 0x2029).ToString(); // PARAGRAPH SEPARATOR

    // Außerhalb eines Kommentars wird jede NL-Variante zu genau einem NewLine-Token —
    // CRLF als ein Token der Länge 2, alle anderen Länge 1.
    static IEnumerable<TestCaseData> PureNewLineCases() {
        yield return new TestCaseData("\n").SetName("pure LF");
        yield return new TestCaseData("\r").SetName("pure CR");
        yield return new TestCaseData("\r\n").SetName("pure CRLF");
        yield return new TestCaseData(Nel).SetName("pure NEL (U+0085)");
        yield return new TestCaseData(Ls).SetName("pure LS (U+2028)");
        yield return new TestCaseData(Ps).SetName("pure PS (U+2029)");
    }

    [Test, TestCaseSource(nameof(PureNewLineCases))]
    public void PureNewLineIsSingleNewLineToken(string eol) {

        var source = "x" + eol;
        var tree   = SyntaxTree.ParseText(source);

        var tokens = Significant(tree);

        Assert.That(tokens.Select(t => t.Type),
                    Is.EqualTo(new[] {SyntaxTokenType.Identifier, SyntaxTokenType.NewLine}));
        Assert.That(tokens[1].ToString(), Is.EqualTo(eol));
        Assert.That(tokens[1].Length,     Is.EqualTo(eol.Length));

        Assert.That(RoundTrip(tree), Is.EqualTo(source));
    }

    // Der Split-Pfad (SplitSingleLineCommenTokens / FindNewLineIndexInSingleLineComment):
    // Ein '//'-Kommentar verschluckt das EOL und wird nur für '\n'/'\r' wieder aufgetrennt.
    // Erwartung je Variante: (EOL, Kommentartext, NewLine-Text  — null = KEIN separates NewLine-Token).
    static IEnumerable<TestCaseData> CommentNewLineCases() {
        yield return new TestCaseData("\n",   "//c",     "\n").SetName("cmt LF");
        yield return new TestCaseData("\r",   "//c",     "\r").SetName("cmt CR");
        yield return new TestCaseData("\r\n", "//c\r",   "\n").SetName("cmt CRLF (\\r bleibt im Kommentar)");
        yield return new TestCaseData(Nel,    "//c" + Nel, null).SetName("cmt NEL (kein Split)");
        yield return new TestCaseData(Ls,     "//c" + Ls,  null).SetName("cmt LS (kein Split)");
        yield return new TestCaseData(Ps,     "//c" + Ps,  null).SetName("cmt PS (kein Split)");
        yield return new TestCaseData("",     "//c",     null).SetName("cmt EOF (kein Split)");
    }

    [Test, TestCaseSource(nameof(CommentNewLineCases))]
    public void SingleLineCommentSplitsOnlyOnCrLf(string eol, string expectedComment, string expectedNewLine) {

        var source = "//c" + eol;
        var tree   = SyntaxTree.ParseText(source);

        var tokens = Significant(tree);

        Assert.That(tokens[0].Type,       Is.EqualTo(SyntaxTokenType.SingleLineComment));
        Assert.That(tokens[0].ToString(), Is.EqualTo(expectedComment));

        if (expectedNewLine == null) {
            Assert.That(tokens.Count, Is.EqualTo(1), "Es wird KEIN separates NewLine-Token erwartet.");
        } else {
            Assert.That(tokens.Count,         Is.EqualTo(2));
            Assert.That(tokens[1].Type,       Is.EqualTo(SyntaxTokenType.NewLine));
            Assert.That(tokens[1].ToString(), Is.EqualTo(expectedNewLine));
        }

        Assert.That(RoundTrip(tree), Is.EqualTo(source));
    }

    #region Infrastructure

    // Alle Token außer dem abschließenden EndOfFile (Länge 0).
    static List<SyntaxToken> Significant(SyntaxTree tree) {
        return tree.Tokens.Where(t => t.Type != SyntaxTokenType.EndOfFile).ToList();
    }

    static string RoundTrip(SyntaxTree tree) {
        var sb = new StringBuilder();
        foreach (var token in tree.Tokens) {
            sb.Append(token.ToString());
        }

        return sb.ToString();
    }

    #endregion

}
