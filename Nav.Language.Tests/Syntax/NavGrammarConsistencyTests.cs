#region Using Directives

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;

#endregion

namespace Nav.Language.Tests;

/// <summary>
/// Sichert ab, dass die zur Compile-Zeit aus den <c>Parse*</c>-EBNF-Fragmenten zusammengesetzte
/// Grammatik (<see cref="NavGrammar"/>) zum tatsächlichen Parser passt. Laufzeit-Gegenstück zu den
/// Generator-Diagnosen NAV001/NAV002 — hier über die <i>echte</i> Engine geprüft — plus die Bindung
/// der Grammatik-Terminale an <see cref="SyntaxFacts"/> und ein Round-Trip je Beispiel-Snippet.
/// </summary>
[TestFixture]
public class NavGrammarConsistencyTests {

    static readonly Regex CommentRegex    = new(@"\(\*.*?\*\)", RegexOptions.Singleline);
    static readonly Regex StringRegex     = new("\"[^\"]*\"");
    static readonly Regex LeftHandSide    = new(@"(?m)^[ \t]*([A-Za-z][A-Za-z0-9]*)[ \t]*::=");
    static readonly Regex IdentifierRegex = new(@"[A-Za-z][A-Za-z0-9]*");

    static readonly string[] CategoricalTerminals = { "Identifier", "StringLiteral", "EOF" };

    [Test]
    public void Grammar_Is_Generated_And_NonEmpty() {
        Assert.That(NavGrammar.Ebnf, Is.Not.Empty, "NavGrammar.Ebnf ist leer — der Quellgenerator hat keine Produktionen erzeugt.");
        Assert.That(NavGrammar.Ebnf, Does.Contain("codeGenerationUnit ::="), "Die Einstiegsregel codeGenerationUnit fehlt.");
        Assert.That(NavGrammar.Rules, Is.Not.Empty);
    }

    [Test]
    public void Every_Rule_Has_A_Production() {

        var defined = DefinedNames(NavGrammar.Ebnf);

        foreach (var ruleName in Enum.GetNames(typeof(NavParser.Rule))) {
            Assert.That(defined, Contains.Item(ToLowerCamel(ruleName)),
                        $"Die Regel '{ruleName}' (NavParser.Rule) hat keine Produktion in NavGrammar.Ebnf.");
        }
    }

    [Test]
    public void Grammar_Is_Closed() {

        var defined = DefinedNames(NavGrammar.Ebnf);

        foreach (var reference in ReferencedNonterminals(NavGrammar.Ebnf)) {
            Assert.That(defined, Contains.Item(reference),
                        $"Das Nichtterminal '{reference}' wird referenziert, ist aber nirgends definiert (Grammatik nicht geschlossen).");
        }
    }

    [Test]
    public void Literal_Terminals_Are_Known_To_SyntaxFacts() {

        foreach (var terminal in QuotedTerminals(NavGrammar.Ebnf)) {

            var known = SyntaxFacts.IsKeyword(terminal) ||
                        terminal.Length == 1 && SyntaxFacts.IsPunctuation(terminal[0]);

            Assert.That(known, Is.True, $"Das literale Terminal \"{terminal}\" ist kein bekanntes Nav-Literal (SyntaxFacts).");
        }
    }

    [Test]
    public void Categorical_Terminals_Are_Known() {

        foreach (var terminal in UppercaseTokens(NavGrammar.Ebnf)) {
            Assert.That(CategoricalTerminals, Contains.Item(terminal),
                        $"Das kategorische Terminal '{terminal}' ist unbekannt (erwartet: {string.Join(", ", CategoricalTerminals)}).");
        }
    }

    [Test]
    public void Every_Rule_Maps_To_Node_And_ParseMethod() {

        var assembly = typeof(Syntax).Assembly;

        foreach (var ruleName in Enum.GetNames(typeof(NavParser.Rule))) {
            Assert.That(assembly.GetType($"Pharmatechnik.Nav.Language.{ruleName}Syntax"), Is.Not.Null,
                        $"Zur Regel '{ruleName}' fehlt der Knotentyp {ruleName}Syntax.");
            Assert.That(typeof(Syntax).GetMethod($"Parse{ruleName}"), Is.Not.Null,
                        $"Zur Regel '{ruleName}' fehlt der Einstieg Syntax.Parse{ruleName}.");
        }
    }

    [Test, TestCaseSource(nameof(SampleRules))]
    public void Sample_RoundTrips(string ruleName, string sample) {

        var rule = (NavParser.Rule)Enum.Parse(typeof(NavParser.Rule), ruleName);
        var tree = NavParser.ParseRule(sample, rule);

        Assert.That(tree.Diagnostics, Is.Empty, $"Das Beispiel-Snippet der Regel '{ruleName}' erzeugt Syntaxfehler: '{sample}'.");
        Assert.That(RoundTrip(tree), Is.EqualTo(sample), $"Das Beispiel-Snippet der Regel '{ruleName}' geht beim Round-Trip verloren.");
    }

    // Beispiel-Snippets je Regel (sofern am Knotentyp via [SampleSyntax] hinterlegt).
    static IEnumerable<TestCaseData> SampleRules() {

        var assembly = typeof(Syntax).Assembly;

        foreach (var ruleName in Enum.GetNames(typeof(NavParser.Rule))) {
            var nodeType = assembly.GetType($"Pharmatechnik.Nav.Language.{ruleName}Syntax");
            var sample   = nodeType == null ? null : SampleSyntax.Of(nodeType);
            if (!string.IsNullOrEmpty(sample)) {
                yield return new TestCaseData(ruleName, sample).SetName($"Sample_RoundTrips({ruleName})");
            }
        }
    }

    #region EBNF-Analyse (unabhängige Reimplementierung — bewusste Redundanz zu den Generator-Diagnosen)

    // Regex.Matches(...).Cast<Match>() ist nur unter net10 redundant — unter net472 implementiert
    // MatchCollection nur das nicht-generische IEnumerable, dort ist der Cast Pflicht (Multi-Target).
    // ReSharper disable RedundantEnumerableCastCall
    static HashSet<string> DefinedNames(string ebnf) {
        var clean = CommentRegex.Replace(ebnf, " ");
        return new HashSet<string>(LeftHandSide.Matches(clean).Cast<Match>().Select(m => m.Groups[1].Value), StringComparer.Ordinal);
    }

    static IEnumerable<string> ReferencedNonterminals(string ebnf) {
        return RightHandSideTokens(ebnf).Where(t => char.IsLower(t[0])).Distinct(StringComparer.Ordinal);
    }

    static IEnumerable<string> UppercaseTokens(string ebnf) {
        return RightHandSideTokens(ebnf).Where(t => char.IsUpper(t[0])).Distinct(StringComparer.Ordinal);
    }

    static IEnumerable<string> RightHandSideTokens(string ebnf) {
        var withoutComments = CommentRegex.Replace(ebnf, " ");
        var withoutStrings  = StringRegex.Replace(withoutComments, " ");
        var rightHandSides  = LeftHandSide.Replace(withoutStrings, " ");
        return IdentifierRegex.Matches(rightHandSides).Cast<Match>().Select(m => m.Value);
    }

    static IEnumerable<string> QuotedTerminals(string ebnf) {
        var withoutComments = CommentRegex.Replace(ebnf, " ");
        return StringRegex.Matches(withoutComments).Cast<Match>()
                          .Select(m => m.Value.Substring(1, m.Value.Length - 2))
                          .Where(t => t.Length > 0)
                          .Distinct(StringComparer.Ordinal);
    }
    // ReSharper restore RedundantEnumerableCastCall

    #endregion

    static string ToLowerCamel(string name) {
        return name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name.Substring(1);
    }

    static string RoundTrip(SyntaxTree tree) {

        var sb = new StringBuilder();

        foreach (var token in tree.Tokens) {

            foreach (var trivia in token.LeadingTrivia) {
                sb.Append(trivia.ToString(tree.SourceText));
            }

            sb.Append(token.ToString());

            foreach (var trivia in token.TrailingTrivia) {
                sb.Append(trivia.ToString(tree.SourceText));
            }
        }

        return sb.ToString();
    }

}
