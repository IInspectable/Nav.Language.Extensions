#region Using Directives

using System;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.GoTo;

#endregion

namespace Nav.Language.Tests.GoTo;

[TestFixture]
public class NavGoToServiceTests {

    [Test]
    public void NodeReference_ResolvesToDeclaration_SameFile() {

        const string src = "task A\n"         +
                           "{\n"              +
                           "    init I1;\n"   +
                           "    exit e1;\n"   +
                           "    I1 --> e1;\n" +
                           "}\n";

        var unit  = ParseModel(src, @"n:\av\a.nav");
        var caret = IndexOfToken(src, "--> e1", "--> "); // 'e1' Referenz in der Transition

        var targets = NavGoToService.GetGoToLocations(unit, caret);

        var expected = IndexOfToken(src, "exit e1", "exit "); // 'e1' Deklaration
        Assert.That(targets.Count,    Is.EqualTo(1));
        Assert.That(targets[0].Start, Is.EqualTo(expected));
    }

    [Test]
    public void Caret_OnKeyword_ReturnsNoTargets() {

        const string src = "task A\n"         +
                           "{\n"              +
                           "    init I1;\n"   +
                           "    exit e1;\n"   +
                           "    I1 --> e1;\n" +
                           "}\n";

        var unit  = ParseModel(src, @"n:\av\a.nav");
        var caret = src.IndexOf("init", StringComparison.Ordinal); // Schlüsselwort, kein GoTo-Symbol

        var targets = NavGoToService.GetGoToLocations(unit, caret);

        Assert.That(targets, Is.Empty);
    }

    [Test]
    public void IncludeDirective_ResolvesToIncludedFile() {

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

        var targets = NavGoToService.GetGoToLocations(unit, caret);

        Assert.That(targets.Count,       Is.EqualTo(1));
        Assert.That(targets[0].FilePath, Is.EqualTo(lib.FilePath));
    }

    [Test]
    public void TaskNode_ResolvesToTaskDeclaration_CrossFile() {

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
        var caret = IndexOfToken(main.Content, "task Sub s", "task "); // Task-Typ 'Sub' am Knoten

        var targets = NavGoToService.GetGoToLocations(unit, caret);

        var expected = IndexOfToken(lib.Content, "task Sub", "task "); // Deklaration 'Sub' in lib.nav
        Assert.That(targets.Count,       Is.EqualTo(1));
        Assert.That(targets[0].FilePath, Is.EqualTo(lib.FilePath));
        Assert.That(targets[0].Start,    Is.EqualTo(expected));
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