#region Using Directives

using NUnit.Framework;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.GoTo;

#endregion

namespace Nav.Language.Tests.GoTo;

/// <summary>
/// Tests des VS-freien <see cref="NavGoToService"/> — der geteilten Engine-Autorität für
/// <b>Nav→Nav</b>-Sprünge (innerhalb und zwischen <c>.nav</c>-Dateien), genutzt von VS-Extension,
/// LSP- und MCP-Host.
/// </summary>
/// <remarks>
/// Trotz der Namensähnlichkeit keine Dopplung der GoTo-Tests in <c>Nav.Language.CodeAnalysis.Tests</c>:
/// jene prüfen den <b>Nav↔C#</b>-Sprung über die Roslyn-Brücke (<c>LocationFinder</c>) in den
/// generierten Code und zurück, <see cref="NavGoToService"/> die dazu <b>disjunkte</b> Richtung, die
/// den <c>.nav</c>-Raum nie verlässt (Sprünge in generierten C#-Code sind hier bewusst ausgeklammert).
/// Je ein Test pro auflösendem <c>GoToTargetResolver</c>-Zweig (Include, Task-Knoten, Knoten-Referenz,
/// Exit-Connection-Point-Referenz), plus „kein Ziel" und Null-Guard.
/// </remarks>
[TestFixture]
public class NavGoToServiceTests {

    [Test]
    public void NodeReference_ResolvesToDeclaration_SameFile() {

        var m = NavMarkup.Parse(
            """
            task A
            {
                init I1;
                exit |decl:e1|;
                I1 --> |ref:e1|;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\a.nav");
        var caret = m.Position("ref"); // 'e1' Referenz in der Transition

        var targets = NavGoToService.GetGoToLocations(unit, caret);

        var expected = m.Position("decl"); // 'e1' Deklaration
        Assert.That(targets.Count,    Is.EqualTo(1));
        Assert.That(targets[0].Start, Is.EqualTo(expected));
    }

    [Test]
    public void Caret_OnKeyword_ReturnsNoTargets() {

        var m = NavMarkup.Parse(
            """
            task A
            {
                |init I1;
                exit e1;
                I1 --> e1;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\a.nav");
        var caret = m.Caret; // Schlüsselwort, kein GoTo-Symbol

        var targets = NavGoToService.GetGoToLocations(unit, caret);

        Assert.That(targets, Is.Empty);
    }

    [Test]
    public void IncludeDirective_ResolvesToIncludedFile() {

        // Caret (|) sitzt im String-Literal "lib.nav".
        var mainMarkup = NavMarkup.Parse(
            """
            taskref "|lib.nav";
            task M
            {
                init I;
                task Sub s;
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
        var caret = mainMarkup.Caret; // im String-Literal

        var targets = NavGoToService.GetGoToLocations(unit, caret);

        Assert.That(targets.Count,       Is.EqualTo(1));
        Assert.That(targets[0].FilePath, Is.EqualTo(lib.FilePath));
    }

    [Test]
    public void TaskNode_ResolvesToTaskDeclaration_CrossFile() {

        // |node:Sub| markiert den Task-Typ am Knoten in main.nav, |decl:Sub| die Deklaration in lib.nav.
        var mainMarkup = NavMarkup.Parse(
            """
            taskref "lib.nav";
            task M
            {
                init I;
                task |node:Sub| s;
                exit e;
                I    --> s;
                s:x  --> e;
            }

            """);

        var libMarkup = NavMarkup.Parse(
            """
            task |decl:Sub|
            {
                init I;
                exit x;
                I --> x;
            }

            """);

        var main = new TestCaseFile {
            FilePath = @"n:\av\main.nav",
            Content  = mainMarkup.Source
        };

        var lib = new TestCaseFile {
            FilePath = @"n:\av\lib.nav",
            Content  = libMarkup.Source
        };

        var unit  = ParseModelWithIncludes(main, lib);
        var caret = mainMarkup.Position("node"); // Task-Typ 'Sub' am Knoten

        var targets = NavGoToService.GetGoToLocations(unit, caret);

        var expected = libMarkup.Position("decl"); // Deklaration 'Sub' in lib.nav
        Assert.That(targets.Count,       Is.EqualTo(1));
        Assert.That(targets[0].FilePath, Is.EqualTo(lib.FilePath));
        Assert.That(targets[0].Start,    Is.EqualTo(expected));
    }

    [Test]
    public void TaskNode_ResolvesToTaskDeclaration_SameFile() {

        // Same-File-Variante zu TaskNode_..._CrossFile: der Task-Typ 'Sub' am Knoten ('|node:Sub|')
        // springt auf die 'task Sub'-Deklaration ('|decl:Sub|') derselben Datei.
        var m = NavMarkup.Parse(
            """
            task |decl:Sub|
            {
                init I;
                exit x;
                I --> x;
            }

            task M
            {
                init I;
                task |node:Sub| s;
                exit e;
                I         --> s;
                s:x       --> e;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\a.nav");
        var caret = m.Position("node"); // Task-Typ 'Sub' am Knoten

        var targets = NavGoToService.GetGoToLocations(unit, caret);

        var expected = m.Position("decl"); // 'task Sub'-Deklaration in derselben Datei
        Assert.That(targets.Count,    Is.EqualTo(1));
        Assert.That(targets[0].Start, Is.EqualTo(expected));
    }

    [Test]
    public void ExitConnectionPointReference_ResolvesToExitDefinition_SameFile() {

        // Deckt den vierten GoToTargetResolver-Zweig ab: die Exit-Connection-Point-Referenz 'x' in
        // der Form 's:x' ('|ref:x|') springt auf die 'exit x;'-Definition ('|decl:x|') des am Knoten
        // 's' referenzierten Tasks 'Sub'.
        var m = NavMarkup.Parse(
            """
            task Sub
            {
                init I;
                exit |decl:x|;
                I --> x;
            }

            task M
            {
                init I;
                task Sub s;
                exit e;
                I         --> s;
                s:|ref:x| --> e;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\a.nav");
        var caret = m.Position("ref"); // Connection-Point 'x' in 's:x'

        var targets = NavGoToService.GetGoToLocations(unit, caret);

        var expected = m.Position("decl"); // 'exit x;'-Definition im Task 'Sub'
        Assert.That(targets.Count,    Is.EqualTo(1));
        Assert.That(targets[0].Start, Is.EqualTo(expected));
    }

    [Test]
    public void NullSymbol_YieldsNoTargets() {

        // Die Einzel-Symbol-Überladung ist die geteilte "wohin springt dieses Symbol"-Autorität; ihr
        // dokumentierter Null-Guard schützt Aufrufer (VS/LSP/MCP), die ein evtl. nicht aufgelöstes
        // Symbol direkt durchreichen.
        var targets = NavGoToService.GetGoToLocations((ISymbol)null);

        Assert.That(targets, Is.Empty);
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