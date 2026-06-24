#region Using Directives

using System;
using System.Linq;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.References;

#endregion

namespace Nav.Language.Tests.References;

[TestFixture]
public class NavReferenceServiceTests {

    const string Nav = "task A\n"         +
                       "{\n"              +
                       "    init I1;\n"   +
                       "    exit e1;\n"   +
                       "\n"               + // Leerzeile — garantiert symbolfreie Position
                       "    I1 --> e1;\n" +
                       "}\n";

    [Test]
    public void Highlight_FromDeclaration_IncludesDeclarationAndReference() {

        var unit  = ParseModel(Nav, @"n:\av\a.nav");
        var caret = IndexOfToken(Nav, "exit e1", "exit "); // auf der Deklaration 'e1'

        var starts = NavReferenceService.GetHighlightSymbols(unit, caret)
                                        .Select(s => s.Location.Start)
                                        .ToList();

        Assert.That(starts, Has.Count.EqualTo(2));
        Assert.That(starts, Does.Contain(IndexOfToken(Nav, "exit e1", "exit ")));  // Deklaration
        Assert.That(starts, Does.Contain(IndexOfToken(Nav, "--> e1",  "--> ")));   // Referenz
    }

    [Test]
    public void Highlight_FromReference_YieldsSameSet() {

        var unit  = ParseModel(Nav, @"n:\av\a.nav");
        var caret = IndexOfToken(Nav, "--> e1", "--> "); // auf der Referenz 'e1'

        var starts = NavReferenceService.GetHighlightSymbols(unit, caret)
                                        .Select(s => s.Location.Start)
                                        .ToList();

        Assert.That(starts, Has.Count.EqualTo(2));
        Assert.That(starts, Does.Contain(IndexOfToken(Nav, "exit e1", "exit ")));
        Assert.That(starts, Does.Contain(IndexOfToken(Nav, "--> e1",  "--> ")));
    }

    [Test]
    public void Highlight_FirstSymbolIsDeclaration() {

        var unit  = ParseModel(Nav, @"n:\av\a.nav");
        var caret = IndexOfToken(Nav, "--> e1", "--> ");

        var symbols = NavReferenceService.GetHighlightSymbols(unit, caret);

        Assert.That(symbols, Is.Not.Empty);
        Assert.That(symbols[0].Location.Start, Is.EqualTo(IndexOfToken(Nav, "exit e1", "exit ")));
    }

    [Test]
    public void Highlight_OnWhitespace_ReturnsEmpty() {

        var unit  = ParseModel(Nav, @"n:\av\a.nav");
        var caret = Nav.IndexOf("e1;\n\n", StringComparison.Ordinal) + "e1;\n".Length; // Leerzeile

        var symbols = NavReferenceService.GetHighlightSymbols(unit, caret);

        Assert.That(symbols, Is.Empty);
    }

    [Test]
    public void Highlight_OnIncludeDirective_IncludesReferencingTaskNode() {

        var main = new TestCaseFile {
            FilePath = @"n:\av\main.nav",
            Content = "taskref \"lib.nav\";\n" +
                      "task M\n"               +
                      "{\n"                    +
                      "    init I;\n"          +
                      "    task Sub s;\n"      +
                      "    exit e;\n"          +
                      "    I    --> s;\n"      +
                      "    s:x  --> e;\n"      +
                      "}\n"
        };

        var lib = new TestCaseFile {
            FilePath = @"n:\av\lib.nav",
            Content = "task Sub\n"     +
                      "{\n"            +
                      "    init I;\n"  +
                      "    exit x;\n"  +
                      "    I --> x;\n" +
                      "}\n"
        };

        var unit  = ParseModelWithIncludes(main, lib);
        var caret = main.Content.IndexOf("lib.nav", StringComparison.Ordinal); // im String-Literal

        var symbols = NavReferenceService.GetHighlightSymbols(unit, caret);

        // Alle Treffer liegen im Haupt-Dokument (documentHighlight ist dateilokal).
        Assert.That(symbols, Is.Not.Empty);
        Assert.That(symbols.All(s => s.Location.FilePath == main.FilePath), Is.True);
        // Der referenzierende Task-Knoten 'Sub' gehört dazu.
        Assert.That(symbols.Select(s => s.Location.Start),
                    Does.Contain(IndexOfToken(main.Content, "task Sub s", "task ")));
    }

    #region Helpers

    static int IndexOfToken(string source, string anchor, string leading) {
        var anchorIndex = source.IndexOf(anchor, StringComparison.Ordinal);
        Assert.That(anchorIndex, Is.GreaterThanOrEqualTo(0), $"Anker '{anchor}' nicht gefunden.");
        return anchorIndex + leading.Length;
    }

    static CodeGenerationUnit ParseModel(string source, string filePath) {
        var syntax = Syntax.ParseCodeGenerationUnit(text: source, filePath: filePath);
        return CodeGenerationUnit.FromCodeGenerationUnitSyntax(syntax);
    }

    static CodeGenerationUnit ParseModelWithIncludes(TestCaseFile main, params TestCaseFile[] includes) {

        var syntaxProvider = new TestSyntaxProvider();
        syntaxProvider.RegisterFile(main);
        foreach (var include in includes) {
            syntaxProvider.RegisterFile(include);
        }

        var syntax = syntaxProvider.GetSyntax(main.FilePath);
        return CodeGenerationUnit.FromCodeGenerationUnitSyntax(syntax, syntaxProvider: syntaxProvider);
    }

    #endregion

}
