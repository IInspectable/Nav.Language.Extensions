#region Using Directives

using System.Linq;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.References;

#endregion

namespace Nav.Language.Tests.References;

[TestFixture]
public class NavReferenceServiceTests {

    // Geteilte Quelle mit im Nav-Code verankerten Positionen:
    //   |decl:e1|  Deklaration von 'e1'
    //   |ref:e1|   Referenz auf 'e1'
    //   |ws:|      garantiert symbolfreie Position (Länge 0 auf der Leerzeile)
    static readonly NavMarkup M = NavMarkup.Parse(
        """
        task A
        {
            init I1;
            exit |decl:e1|;
        |ws:|
            I1 --> |ref:e1|;
        }

        """);

    [Test]
    public void Highlight_FromDeclaration_IncludesDeclarationAndReference() {

        var unit  = ParseModel(M.Source, @"n:\av\a.nav");
        var caret = M.Position("decl"); // auf der Deklaration 'e1'

        var starts = NavReferenceService.GetHighlightSymbols(unit, caret)
                                        .Select(s => s.Location.Start)
                                        .ToList();

        Assert.That(starts, Has.Count.EqualTo(2));
        Assert.That(starts, Does.Contain(M.Position("decl"))); // Deklaration
        Assert.That(starts, Does.Contain(M.Position("ref")));  // Referenz
    }

    [Test]
    public void Highlight_FromReference_YieldsSameSet() {

        var unit  = ParseModel(M.Source, @"n:\av\a.nav");
        var caret = M.Position("ref"); // auf der Referenz 'e1'

        var starts = NavReferenceService.GetHighlightSymbols(unit, caret)
                                        .Select(s => s.Location.Start)
                                        .ToList();

        Assert.That(starts, Has.Count.EqualTo(2));
        Assert.That(starts, Does.Contain(M.Position("decl")));
        Assert.That(starts, Does.Contain(M.Position("ref")));
    }

    [Test]
    public void Highlight_FirstSymbolIsDeclaration() {

        var unit  = ParseModel(M.Source, @"n:\av\a.nav");
        var caret = M.Position("ref");

        var symbols = NavReferenceService.GetHighlightSymbols(unit, caret);

        Assert.That(symbols, Is.Not.Empty);
        Assert.That(symbols[0].Location.Start, Is.EqualTo(M.Position("decl")));
    }

    [Test]
    public void Highlight_OnWhitespace_ReturnsEmpty() {

        var unit  = ParseModel(M.Source, @"n:\av\a.nav");
        var caret = M.Position("ws"); // Leerzeile — symbolfrei

        var symbols = NavReferenceService.GetHighlightSymbols(unit, caret);

        Assert.That(symbols, Is.Empty);
    }

    [Test]
    public void Highlight_OnIncludeDirective_IncludesReferencingTaskNode() {

        // Caret (|) sitzt im String-Literal "lib.nav"; |node:Sub| markiert den referenzierenden
        // Task-Knoten, dessen Position wir als Treffer erwarten.
        var mainMarkup = NavMarkup.Parse(
            """
            taskref "|lib.nav";
            task M
            {
                init I;
                task |node:Sub| s;
                exit e;
                I    --> s;
                s:x  --> e;
            }

            """);

        var main = new TestCaseFile {
            FilePath = @"n:\av\main.nav",
            Content  = mainMarkup.Source
        };

        var lib = new TestCaseFile {
            FilePath = @"n:\av\lib.nav",
            Content  = """
                       task Sub
                       {
                           init I;
                           exit x;
                           I --> x;
                       }

                       """
        };

        var unit  = ParseModelWithIncludes(main, lib);
        var caret = mainMarkup.Caret; // im String-Literal "lib.nav"

        var symbols = NavReferenceService.GetHighlightSymbols(unit, caret);

        // Alle Treffer liegen im Haupt-Dokument (documentHighlight ist dateilokal).
        Assert.That(symbols, Is.Not.Empty);
        Assert.That(symbols.All(s => s.Location.FilePath == main.FilePath), Is.True);
        // Der referenzierende Task-Knoten 'Sub' gehört dazu.
        Assert.That(symbols.Select(s => s.Location.Start),
                    Does.Contain(mainMarkup.Position("node")));
    }

    #region Helpers

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
