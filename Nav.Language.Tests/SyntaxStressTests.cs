using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Pharmatechnik.Nav.Language;

// ReSharper disable PossibleNullReferenceException => Dann soll es so sein. Test wird dann eh rot

namespace Nav.Language.Tests; 

[TestFixture]
public class SyntaxStressTests {

    [Test]
    public void IncompleteSyntaxTest() {
        var syntax = Syntax.ParseTaskDefinition("task F [base {}");

        Assert.That(syntax.CodeBaseDeclaration, Is.Not.Null);

        // Bewusste Robustheitsprüfung: der Test verifiziert die non-null-Zusage der Annotation zur Laufzeit.
        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        var nullSyntaxen = syntax.CodeBaseDeclaration.BaseTypes.Where(b => b == null);
        Assert.That(nullSyntaxen.Any(), Is.False);
    }

    [Test]
    public void SimulatedRandomTypingTest() {
        var source = Resources.AllRules;

        List<int> indexes = RandomShuffle(Enumerable.Range(0, source.Length));
        for (int count = 1; count <= source.Length; count++) {
          
            var sb = new StringBuilder();
            foreach(var index in indexes) {
                sb.Append(source[index]);
            }

            var text = sb.ToString();
            var tree =SyntaxTree.ParseText(text);

            CheckNonNullChild(tree.Root);
            CheckRoundTrip(tree, text);
        }
    }

    [Test]
    public void SimulatedTypingTest() {
        var source = Resources.AllRules;

        for (int index = 1; index <= source.Length; index++) {
                
            var text = source.Substring(0, source.Length -index);
            var tree = SyntaxTree.ParseText(text);

            CheckNonNullChild(tree.Root);
            CheckRoundTrip(tree, text);
        }
    }

    void CheckNonNullChild(SyntaxNode node) {
        Assert.That(node, Is.Not.Null);
        foreach (var child in node.ChildNodes()) {
            CheckNonNullChild(child);
        }
    }

    // Full-Fidelity-Invariante: Die sortierte Token-Liste plus ihre angehängte Trivia deckt den
    // Quelltext lückenlos ab — je Token Leading-Trivia, eigener Text und Trailing-Trivia konkateniert
    // ergeben exakt den Originaltext zurück (Trivia liegt nicht mehr als eigenes Token im Strom).
    static void CheckRoundTrip(SyntaxTree tree, string text) {
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

        Assert.That(sb.ToString(), Is.EqualTo(text));
    }

    List<int> RandomShuffle(IEnumerable<int> source) {
        var random = new Random();

        return source.Select(i => new KeyValuePair<int, int>(random.Next(), i))
                     .OrderBy(i => i.Key)
                     .Select(i => i.Value)
                     .ToList();
    }
}