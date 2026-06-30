#region Using Directives

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;

#endregion

namespace Nav.Language.Tests;

/// <summary>
/// Pinnt das <b>Lex</b>-Verhalten des heutigen Lexers isoliert vom Parser — Token-Sequenzen für
/// kniffige Unicode- und Längstmatch-Kanten, an denen der künftige handgeschriebene Lexer am
/// ehesten abweicht (Umlaut-Identifier, Kanten-Keywords <c>o-&gt;</c>/<c>--&gt;</c>/<c>==&gt;</c>
/// gegen ihre Präfixe, Zs-Whitespace-Klassen, String-Literale mit/ohne Abschluss).
/// <para/>
/// Bewusst <b>Inline-Tests, kein Datei-Korpus</b> (wie <see cref="SyntaxNewLineTests"/>): Sonderzeichen
/// werden über numerische <c>\u….</c>-Escapes bzw. <c>(char)0x…</c>-Casts gebildet und stehen NIE als
/// literale Bytes in dieser Datei — damit ist die Eingabe immun gegen jede Normalisierung durch git
/// oder Editoren. Die Zeilenende-Varianten (<c>\n</c>/<c>\r</c>/<c>\r\n</c> sowie NEL/LS/PS) sind
/// bereits in <see cref="SyntaxNewLineTests"/> erschöpfend gepinnt und werden hier nicht wiederholt.
/// </summary>
[TestFixture]
public class SyntaxLexerTests {

    static readonly string Nbsp = ((char)0x00A0).ToString(); // NO-BREAK SPACE         (Zs)
    static readonly string EmSp = ((char)0x2003).ToString(); // EM SPACE               (Zs)
    static readonly string IdSp = ((char)0x3000).ToString(); // IDEOGRAPHIC SPACE      (Zs)
    static readonly string Tab  = ((char)0x0009).ToString(); // HORIZONTAL TAB
    static readonly string Vt   = ((char)0x000B).ToString(); // VERTICAL TAB
    static readonly string Ff   = ((char)0x000C).ToString(); // FORM FEED
    static readonly string Nel  = ((char)0x0085).ToString(); // NEXT LINE              (NL, aber KEIN StringLiteral-Trenner)
    static readonly string Acut = ((char)0x00E9).ToString(); // 'é' (U+00E9) — KEIN Nav-Letter

    // ----------------------------------------------------------------------------------------------
    // Identifier: Buchstaben (inkl. der sieben erlaubten Umlaute), '_', Ziffern und '.' bilden EINEN
    // Identifier-Token. Nav kennt keine eigenen Zahl-Token — auch reine Ziffern/führende Ziffern und
    // ein einzelner '.' werden zum Identifier.
    // ----------------------------------------------------------------------------------------------
    static IEnumerable<TestCaseData> SingleIdentifierCases() {
        yield return new TestCaseData("ä").SetName("ident 'ä'");
        yield return new TestCaseData("Ö").SetName("ident 'Ö'");
        yield return new TestCaseData("ß").SetName("ident 'ß'");
        yield return new TestCaseData("Ärzte").SetName("ident 'Ärzte'");
        yield return new TestCaseData("Maß").SetName("ident 'Maß'");
        yield return new TestCaseData("_foo").SetName("ident '_foo'");
        yield return new TestCaseData("a.b.c").SetName("ident 'a.b.c' (Punkt ist Identifier-Zeichen)");
        yield return new TestCaseData("a_1.2").SetName("ident 'a_1.2'");
        yield return new TestCaseData("123").SetName("ident '123' (reine Ziffern, kein Zahl-Token)");
        yield return new TestCaseData("1abc").SetName("ident '1abc' (führende Ziffer)");
        yield return new TestCaseData(".").SetName("ident '.' (einzelner Punkt)");
    }

    [Test, TestCaseSource(nameof(SingleIdentifierCases))]
    public void IdentifierCharactersFormSingleIdentifier(string source) {

        var tokens = Lex(source);

        Assert.That(tokens.Select(t => t.Type), Is.EqualTo(new[] { SyntaxTokenType.Identifier }));
        Assert.That(Text(tokens[0], source),    Is.EqualTo(source));
        Assert.That(RoundTrip(source),          Is.EqualTo(source));
    }

    // ----------------------------------------------------------------------------------------------
    // Keyword vs. Identifier: Längstmatch + Regel-Reihenfolge. Ein Keyword gewinnt nur, wenn es die
    // gesamte Identifier-Sequenz exakt deckt — sobald ein Identifier-Zeichen folgt, ist es ein
    // Identifier.
    // ----------------------------------------------------------------------------------------------
    static IEnumerable<TestCaseData> KeywordBoundaryCases() {
        yield return new TestCaseData("task",    SyntaxTokenType.TaskKeyword).SetName("kw 'task'");
        yield return new TestCaseData("taskref", SyntaxTokenType.TaskrefKeyword).SetName("kw 'taskref'");
        yield return new TestCaseData("init",    SyntaxTokenType.InitKeyword).SetName("kw 'init'");
        yield return new TestCaseData("tasks",   SyntaxTokenType.Identifier).SetName("ident 'tasks' (Keyword + Suffix)");
        yield return new TestCaseData("task1",   SyntaxTokenType.Identifier).SetName("ident 'task1'");
        yield return new TestCaseData("initial", SyntaxTokenType.Identifier).SetName("ident 'initial'");
    }

    [Test, TestCaseSource(nameof(KeywordBoundaryCases))]
    public void KeywordWinsOnlyOnFullMatch(string source, SyntaxTokenType expected) {

        var tokens = Lex(source);

        Assert.That(tokens.Select(t => t.Type), Is.EqualTo(new[] { expected }));
        Assert.That(Text(tokens[0], source),    Is.EqualTo(source));
        Assert.That(RoundTrip(source),          Is.EqualTo(source));
    }

    // ----------------------------------------------------------------------------------------------
    // Kanten-Keywords gegen ihre Präfixe. 'o->', '-->' und '==>' sind je EIN Token; ihre echten
    // Teilstücke ('o', '->', '-', '=', '==') zerfallen — ein einzelnes '-' oder '=' ist ein
    // Unknown-Token (Trivia-Kanal), '>' ein GreaterThan.
    // ----------------------------------------------------------------------------------------------
    static IEnumerable<TestCaseData> EdgeTokenCases() {
        yield return new TestCaseData("o->",  new[] { SyntaxTokenType.ModalEdgeKeyword }).SetName("edge 'o->'");
        yield return new TestCaseData("o->I", new[] { SyntaxTokenType.ModalEdgeKeyword, SyntaxTokenType.Identifier }).SetName("edge 'o->I'");
        yield return new TestCaseData("oXyz", new[] { SyntaxTokenType.Identifier }).SetName("ident 'oXyz' (kein o->)");
        yield return new TestCaseData("o",    new[] { SyntaxTokenType.Identifier }).SetName("ident 'o'");
        yield return new TestCaseData("-->",  new[] { SyntaxTokenType.GoToEdgeKeyword }).SetName("edge '-->'");
        yield return new TestCaseData("->",   new[] { SyntaxTokenType.Unknown, SyntaxTokenType.GreaterThan }).SetName("'->' zerfällt in Unknown + GreaterThan");
        yield return new TestCaseData("-",    new[] { SyntaxTokenType.Unknown }).SetName("'-' ist Unknown");
        yield return new TestCaseData("==>",  new[] { SyntaxTokenType.NonModalEdgeKeyword }).SetName("edge '==>'");
        yield return new TestCaseData("=",    new[] { SyntaxTokenType.Unknown }).SetName("'=' ist Unknown");
        yield return new TestCaseData("==",   new[] { SyntaxTokenType.Unknown, SyntaxTokenType.Unknown }).SetName("'==' zerfällt in zwei Unknown");
    }

    [Test, TestCaseSource(nameof(EdgeTokenCases))]
    public void EdgeKeywordsVersusTheirPrefixes(string source, SyntaxTokenType[] expected) {
        AssertSequence(source, expected);
    }

    // ----------------------------------------------------------------------------------------------
    // Nicht-Nav-Letter (z.B. 'é' U+00E9) gehören NICHT zur Identifier-Klasse und trennen den
    // Identifier — das Restzeichen wird ein Unknown-Token.
    // ----------------------------------------------------------------------------------------------
    static IEnumerable<TestCaseData> NonLetterCases() {
        yield return new TestCaseData(Acut,         new[] { SyntaxTokenType.Unknown }).SetName("'é' allein ist Unknown");
        yield return new TestCaseData("caf" + Acut, new[] { SyntaxTokenType.Identifier, SyntaxTokenType.Unknown }).SetName("'café' trennt nach 'caf'");
    }

    [Test, TestCaseSource(nameof(NonLetterCases))]
    public void NonNavLetterSplitsIdentifier(string source, SyntaxTokenType[] expected) {
        AssertSequence(source, expected);
    }

    // ----------------------------------------------------------------------------------------------
    // Whitespace-Klasse: jede Zs-Variante sowie Tab/VT/FF ist Whitespace; aufeinanderfolgender
    // Whitespace verschmilzt zu EINEM Token (WS+).
    // ----------------------------------------------------------------------------------------------
    static IEnumerable<TestCaseData> WhitespaceCases() {
        yield return new TestCaseData(" ").SetName("ws SPACE (U+0020)");
        yield return new TestCaseData(Nbsp).SetName("ws NBSP (U+00A0)");
        yield return new TestCaseData(EmSp).SetName("ws EM SPACE (U+2003)");
        yield return new TestCaseData(IdSp).SetName("ws IDEOGRAPHIC SPACE (U+3000)");
        yield return new TestCaseData(Tab).SetName("ws TAB (U+0009)");
        yield return new TestCaseData(Vt).SetName("ws VT (U+000B)");
        yield return new TestCaseData(Ff).SetName("ws FF (U+000C)");
        yield return new TestCaseData(" " + Tab + Nbsp).SetName("ws-Lauf verschmilzt zu einem Token");
    }

    [Test, TestCaseSource(nameof(WhitespaceCases))]
    public void WhitespaceClassFormsSingleWhitespaceToken(string ws) {

        var source = "a" + ws + "b";
        var tokens = Lex(source);

        Assert.That(tokens.Select(t => t.Type),
                    Is.EqualTo(new[] { SyntaxTokenType.Identifier, SyntaxTokenType.Whitespace, SyntaxTokenType.Identifier }));
        Assert.That(Text(tokens[1], source), Is.EqualTo(ws));
        Assert.That(tokens[1].Length,        Is.EqualTo(ws.Length));
        Assert.That(RoundTrip(source),       Is.EqualTo(source));
    }

    // ----------------------------------------------------------------------------------------------
    // String-Literale. Ein StringLiteral braucht ein schließendes '"'. Fehlt es, zerfällt das
    // öffnende '"' in ein Unknown-Token und der Rest wird normal weiter gelext. Die Trenner-Zeichen
    // sind nur '"', CR, LF, LS und PS — NICHT NEL (U+0085): ein NEL bleibt INNERHALB des Strings.
    // ----------------------------------------------------------------------------------------------
    static IEnumerable<TestCaseData> StringLiteralCases() {
        yield return new TestCaseData("\"\"",              new[] { SyntaxTokenType.StringLiteral }).SetName("leeres Literal \"\"");
        yield return new TestCaseData("\"abc\"",           new[] { SyntaxTokenType.StringLiteral }).SetName("Literal \"abc\"");
        yield return new TestCaseData("\"abc",             new[] { SyntaxTokenType.Unknown, SyntaxTokenType.Identifier }).SetName("unterminiert \"abc");
        yield return new TestCaseData("\"a\"b\"",          new[] { SyntaxTokenType.StringLiteral, SyntaxTokenType.Identifier, SyntaxTokenType.Unknown }).SetName("\"a\"b\" — zweites Literal bleibt offen");
        yield return new TestCaseData("\"a\nb\"",          new[] { SyntaxTokenType.Unknown, SyntaxTokenType.Identifier, SyntaxTokenType.NewLine, SyntaxTokenType.Identifier, SyntaxTokenType.Unknown }).SetName("LF beendet das Literal vorzeitig");
        yield return new TestCaseData("\"a" + Nel + "b\"", new[] { SyntaxTokenType.StringLiteral }).SetName("NEL bleibt im Literal");
    }

    [Test, TestCaseSource(nameof(StringLiteralCases))]
    public void StringLiteralTokenization(string source, SyntaxTokenType[] expected) {
        AssertSequence(source, expected);
    }

    // ----------------------------------------------------------------------------------------------
    // Entartete Eingaben: leer, nur Whitespace, nur Kommentar.
    // ----------------------------------------------------------------------------------------------
    static IEnumerable<TestCaseData> DegenerateCases() {
        yield return new TestCaseData("",         Array.Empty<SyntaxTokenType>()).SetName("leerer Input (keine signifikanten Token)");
        yield return new TestCaseData("   ",      new[] { SyntaxTokenType.Whitespace }).SetName("nur Whitespace");
        yield return new TestCaseData("//c",      new[] { SyntaxTokenType.SingleLineComment }).SetName("nur Zeilenkommentar");
        yield return new TestCaseData("/*c*/",    new[] { SyntaxTokenType.MultiLineComment }).SetName("nur Blockkommentar");
        yield return new TestCaseData("/*a\nb*/", new[] { SyntaxTokenType.MultiLineComment }).SetName("mehrzeiliger Blockkommentar");
    }

    [Test, TestCaseSource(nameof(DegenerateCases))]
    public void DegenerateInputs(string source, SyntaxTokenType[] expected) {
        AssertSequence(source, expected);
    }

    #region Infrastructure

    static void AssertSequence(string source, SyntaxTokenType[] expected) {

        Assert.That(Lex(source).Select(t => t.Type), Is.EqualTo(expected));
        Assert.That(RoundTrip(source), Is.EqualTo(source),
                    "Der Lex-Output (inkl. Trivia) muss den Quelltext lückenlos reproduzieren.");
    }

    // Reiner Lex-Output (alle RawToken außer dem abschließenden EndOfFile) — direkt aus dem Lexer, da
    // Trivia (Whitespace/Zeilenende/Kommentar) nicht mehr im flachen Parser-Strom liegt.
    static List<RawToken> Lex(string source) {
        return NavLexer.Lex(source).Where(t => t.Type != SyntaxTokenType.EndOfFile).ToList();
    }

    static string Text(RawToken token, string source) {
        return source.Substring(token.Start, token.Length);
    }

    static string RoundTrip(string source) {
        var sb = new StringBuilder();
        foreach (var token in NavLexer.Lex(source)) {
            sb.Append(source.Substring(token.Start, token.Length));
        }

        return sb.ToString();
    }

    #endregion

}