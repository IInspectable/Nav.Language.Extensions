#region Using Directives

using System;
using System.Linq;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.CodeActions;
using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Nav.Language.Tests.CodeActions;

[TestFixture]
public class NavCodeActionServiceTests {

    static readonly TextEditorSettings Settings = new(tabSize: 4, newLine: "\n");

    [Test]
    public void RemoveUnusedNodes_OfferedAndRemovesUnusedViewNode() {

        const string nav = "task A\n"        +
                           "{\n"             +
                           "    init I1;\n"  +
                           "    exit e1;\n"  +
                           "    view v;\n"   +
                           "\n"              +
                           "    I1 --> e1;\n" +
                           "}\n";

        var unit  = ParseModel(nav, @"n:\av\a.nav");
        var caret = CaretAfter(nav, "view "); // auf der Knoten-Deklaration 'v'

        var actions = NavCodeActionService.GetCodeActions(unit, Caret(caret), Settings);

        var fix = actions.SingleOrDefault(a => a.Title == "Remove Unused Nodes");
        Assert.That(fix, Is.Not.Null, "Erwartete Aktion 'Remove Unused Nodes' fehlt.");

        var actual = Apply(nav, fix);
        Assert.That(actual, Does.Not.Contain("view v;"));
        Assert.That(actual, Does.Contain("I1 --> e1;"));
    }

    [Test]
    public void RemoveUnusedTaskDeclaration_OfferedAndRemovesDeclaration() {

        const string nav = "taskref A\n" +
                           "{\n"          +
                           "    init i;\n" +
                           "    exit e;\n" +
                           "}\n"          +
                           "\n"           +
                           "task B\n"     +
                           "{\n"          +
                           "    init i;\n" +
                           "    exit e;\n" +
                           "    i --> e;\n" +
                           "}\n";

        var unit  = ParseModel(nav, @"n:\av\a.nav");
        var caret = CaretAfter(nav, "taskref "); // auf der Task-Deklaration 'A'

        var actions = NavCodeActionService.GetCodeActions(unit, Caret(caret), Settings);

        var fix = actions.SingleOrDefault(a => a.Title == "Remove Unused Task Declaration");
        Assert.That(fix, Is.Not.Null, "Erwartete Aktion 'Remove Unused Task Declaration' fehlt.");

        var actual = Apply(nav, fix);
        Assert.That(actual, Does.Not.Contain("taskref A"));
        Assert.That(actual, Does.Contain("task B"));
    }

    [Test]
    public void AddMissingSemicolon_OfferedAndInsertsSemicolon() {

        const string nav = "taskref \"Other.nav\"\n" + // fehlendes ';'
                           "\n"                        +
                           "task A\n"                  +
                           "{\n"                       +
                           "    init I1;\n"            +
                           "    exit e1;\n"            +
                           "    I1 --> e1;\n"          +
                           "}\n";

        var unit  = ParseModel(nav, @"n:\av\a.nav");
        var caret = CaretAfter(nav, "taskref "); // in der Include-Direktive

        var actions = NavCodeActionService.GetCodeActions(unit, Caret(caret), Settings);

        var fix = actions.SingleOrDefault(a => a.Title == "Add missing ';' on Include Directives");
        Assert.That(fix, Is.Not.Null, "Erwartete Aktion zum Ergänzen des ';' fehlt.");

        var actual = Apply(nav, fix);
        Assert.That(actual, Does.Contain("taskref \"Other.nav\";"));
    }

    [Test]
    public void IntroduceChoice_OfferedAndInsertsChoice() {

        const string nav = "task A\n"         +
                           "{\n"              +
                           "    init I1;\n"   +
                           "    exit e1;\n"   +
                           "    view v;\n"    +
                           "\n"               +
                           "    I1 --> v;\n"  +
                           "    v  --> e1;\n" +
                           "}\n";

        var unit  = ParseModel(nav, @"n:\av\a.nav");
        var caret = CaretAfter(nav, "I1 --> "); // auf der Ziel-Referenz 'v'

        var actions = NavCodeActionService.GetCodeActions(unit, Caret(caret), Settings);

        var fix = actions.SingleOrDefault(a => a.Title == "Introduce Choice");
        Assert.That(fix, Is.Not.Null, "Erwartete Aktion 'Introduce Choice' fehlt.");

        var actual = Apply(nav, fix);
        Assert.That(actual, Does.Contain("choice Choice_v;"));
    }

    [Test]
    public void EmptyCaret_OnWhitespace_ReturnsNoActions() {

        const string nav = "task A\n"        +
                           "{\n"             +
                           "    init I1;\n"  +
                           "    exit e1;\n"  +
                           "\n"              + // Leerzeile — symbolfreie Position
                           "    I1 --> e1;\n" +
                           "}\n";

        var unit  = ParseModel(nav, @"n:\av\a.nav");
        var caret = nav.IndexOf("e1;\n\n", StringComparison.Ordinal) + "e1;\n".Length; // Leerzeile

        var actions = NavCodeActionService.GetCodeActions(unit, Caret(caret), Settings);

        Assert.That(actions, Is.Empty);
    }

    #region Helpers

    static int CaretAfter(string source, string leading) {
        var index = source.IndexOf(leading, StringComparison.Ordinal);
        Assert.That(index, Is.GreaterThanOrEqualTo(0), $"Anker '{leading}' nicht gefunden.");
        return index + leading.Length;
    }

    static TextExtent Caret(int offset) => TextExtent.FromBounds(offset, offset);

    static string Apply(string text, NavCodeAction action) {
        var writer = new TextChangeWriter();
        return writer.ApplyTextChanges(text, action.TextChanges);
    }

    static CodeGenerationUnit ParseModel(string source, string filePath) {
        var syntax = Syntax.ParseCodeGenerationUnit(text: source, filePath: filePath);
        return CodeGenerationUnit.FromCodeGenerationUnitSyntax(syntax);
    }

    #endregion

}
