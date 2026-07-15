using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.Internal;

namespace Nav.Language.Tests;

/// <summary>
/// Gemeinsame Fall-Quelle der Snippet-Parser-Tests: jeder per-Regel-Einstieg <c>Syntax.ParseXxx</c> liefert
/// über seinen Rückgabetyp den zugehörigen <see cref="SyntaxNode"/>-Wurzeltyp. Zur Laufzeit über Reflexion
/// aufgezählt (löst die früheren T4-Templates <c>SyntaxTest.tt</c>/<c>ParseEmptyStringTests.tt</c>/
/// <c>TokenPropertyNameTests.tt</c> ab, die dieselbe Aufzählung zur Generierungszeit machten).
/// </summary>
static class SyntaxSnippetParsers {

    /// <summary>Alle <c>Syntax.ParseXxx</c>-Einstiege, je einer pro Grammatikregel, als NUnit-Testfälle.</summary>
    public static IEnumerable<TestCaseData> All() {
        return typeof(Syntax).GetMethods(BindingFlags.Public | BindingFlags.Static)
                             .Where(m => m.Name.StartsWith("Parse", StringComparison.Ordinal) &&
                                         typeof(SyntaxNode).IsAssignableFrom(m.ReturnType))
                             .OrderBy(m => m.ReturnType.Name, StringComparer.Ordinal)
                             .Select(m => new TestCaseData(m).SetArgDisplayNames(m.ReturnType.Name));
    }

    /// <summary>Ruft den per-Regel-Einstieg <paramref name="parseMethod"/> auf <paramref name="text"/> auf.</summary>
    public static SyntaxNode Parse(MethodInfo parseMethod, string text) {
        return (SyntaxNode) parseMethod.Invoke(null, [text, null, CancellationToken.None])!;
    }

    /// <summary>Die öffentlichen <see cref="SyntaxToken"/>-Properties des Knotentyps <paramref name="nodeType"/>.</summary>
    public static IReadOnlyList<PropertyInfo> TokenProperties(Type nodeType) {
        return nodeType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                       .Where(p => p.PropertyType == typeof(SyntaxToken))
                       .ToList();
    }

}

/// <summary>
/// Prüft für jeden per-Regel-Einstieg, dass sein hinterlegtes <see cref="SampleSyntaxAttribute"/>-Beispiel
/// fehlerfrei parst und keinen als fehlend markierten Kind-Token hinterlässt.
/// </summary>
[TestFixture]
public class SyntaxTests {

    [TestCaseSource(typeof(SyntaxSnippetParsers), nameof(SyntaxSnippetParsers.All))]
    public void SampleSyntaxParsesWithoutDiagnostics(MethodInfo parseMethod) {

        var nodeType = parseMethod.ReturnType;
        var sample   = SampleSyntax.Of(nodeType);
        if (sample == null) {
            Assert.Ignore($"{nodeType.Name} hat kein SampleSyntaxAttribute (oder keine Beispielsyntax).");
            return;
        }

        var node = SyntaxSnippetParsers.Parse(parseMethod, sample);

        Assert.That(node.SyntaxTree.Diagnostics, Is.Empty,
                    $"Die Beispiel-Syntax von {nodeType.Name} führt zu Syntaxfehlern:\r\n" +
                    string.Join("\r\n", node.SyntaxTree.Diagnostics));

        foreach (var token in node.ChildTokens()) {
            Assert.That(token.IsMissing, Is.False, $"Ein Token ist als 'fehlend' gekennzeichnet:\r\n{token}");
        }
    }

}

/// <summary>
/// Prüft für jeden per-Regel-Einstieg, dass beim Parsen der leeren Eingabe jede <see cref="SyntaxToken"/>-
/// Property (samt ihrem Extent) als fehlend gekennzeichnet ist.
/// </summary>
[TestFixture]
public class ParseEmptyStringTests {

    [TestCaseSource(typeof(SyntaxSnippetParsers), nameof(SyntaxSnippetParsers.All))]
    public void EmptyInputYieldsMissingTokens(MethodInfo parseMethod) {

        var nodeType   = parseMethod.ReturnType;
        var tokenProps = SyntaxSnippetParsers.TokenProperties(nodeType);
        if (tokenProps.Count == 0) {
            Assert.Pass($"{nodeType.Name} hat keine SyntaxToken-Properties.");
            return;
        }

        var node = SyntaxSnippetParsers.Parse(parseMethod, "");

        foreach (var prop in tokenProps) {
            var token = (SyntaxToken) prop.GetValue(node)!;
            Assert.That(token.IsMissing,        Is.True, $"Das Token '{prop.Name}' ({token}) sollte als 'fehlend' gekennzeichnet sein.");
            Assert.That(token.Extent.IsMissing, Is.True, $"Extent des Token '{prop.Name}' ({token}) sollte als 'fehlend' gekennzeichnet sein.");
        }
    }

}

/// <summary>
/// Sanity-Check der Namenskonvention: jede <see cref="SyntaxToken"/>-Property soll so heißen wie der
/// Token-Typ, den sie im geparsten Beispiel trägt (z.B. eine Property mit dem <c>DoKeyword</c>-Token heißt
/// <c>DoKeyword</c>). Properties mit <see cref="SuppressCodeSanityCheckAttribute"/> sind ausgenommen.
/// </summary>
[TestFixture]
[Category("Tests noch nicht fertig.")]
public class TokenPropertyNameTests {

    [TestCaseSource(typeof(SyntaxSnippetParsers), nameof(SyntaxSnippetParsers.All))]
    public void TokenPropertyNameMatchesTokenType(MethodInfo parseMethod) {

        var nodeType   = parseMethod.ReturnType;
        var tokenProps = SyntaxSnippetParsers.TokenProperties(nodeType);
        if (tokenProps.Count == 0) {
            Assert.Pass($"{nodeType.Name} hat keine SyntaxToken-Properties.");
            return;
        }

        var sample = SampleSyntax.Of(nodeType);
        if (sample == null) {
            Assert.Ignore($"{nodeType.Name} hat kein SampleSyntaxAttribute (oder keine Beispielsyntax).");
            return;
        }

        var node = SyntaxSnippetParsers.Parse(parseMethod, sample);

        foreach (var prop in tokenProps) {
            if (Attribute.IsDefined(prop, typeof(SuppressCodeSanityCheckAttribute))) {
                continue;
            }

            var tokenType = ((SyntaxToken) prop.GetValue(node)!).Type;
            Assert.That(prop.Name, Is.EqualTo(tokenType.ToString()),
                        $"Der Name der Eigenschaft '{prop.Name}' sollte '{tokenType}' lauten.");
        }
    }

}
