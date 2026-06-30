using System;
using System.Linq;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.Text;

namespace Nav.Language.Tests;

[TestFixture]
public class ClassifiedTextExtensionsTests {

    [Test]
    public void GetClassifiedText_PreservesWhitespaceAndComment() {

        const string nav = "task A\n"                    +
                           "{\n"                         +
                           "    init I1;\n"              +
                           "    exit e1;\n"              +
                           "    I1 --> e1;   // Kommentar\n" +
                           "}\n";

        var tree       = SyntaxTree.ParseText(nav);
        var line       = tree.SourceText.GetTextLineAtPosition(nav.IndexOf("I1 --> e1;", StringComparison.Ordinal));
        var lineExtent = line.ExtentWithoutLineEndings;

        var parts = tree.GetClassifiedText(lineExtent).ToList();

        // Verlustfrei: die klassifizierten Stücke ergeben zusammen exakt den Zeilentext (inkl. Einrückung,
        // Zwischenräume und Kommentar) — beweist, dass kein Trivia-Text verloren geht, auch nachdem die Trivia
        // den flachen Token-Strom verlassen hat.
        Assert.That(parts.JoinText(), Is.EqualTo(tree.SourceText.Substring(lineExtent)));
        Assert.That(parts.Any(p => p.Classification == TextClassification.Comment),    Is.True, "Kommentar-Stück fehlt.");
        Assert.That(parts.Any(p => p.Classification == TextClassification.Whitespace), Is.True, "Whitespace-Stück fehlt.");
    }
}
