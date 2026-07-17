#region Using Directives

using System.Linq;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.Rename;
using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Nav.Language.Tests.Rename;

[TestFixture]
public class NavRenameServiceTests {

    //   |kw:|    Position im Keyword 'exit'
    //   |decl:e1| Deklaration von 'e1'
    //   |ws:|    Leerzeile — garantiert symbolfreie Position
    //   |ref:e1| Referenz auf 'e1'
    static readonly NavMarkup M = NavMarkup.Parse(
        """
        task A
        {
            init I1;
            e|kw:|xit |decl:e1|;
        |ws:|
            I1 --> |ref:e1|;
        }

        """);

    static readonly TextEditorSettings Settings = new(tabSize: 4, newLine: "\n");

    [Test]
    public void Rename_FromDeclaration_RenamesDeclarationAndReference() {

        var unit  = ParseModel(M.Source, @"n:\av\a.nav");
        var caret = M.Position("decl"); // auf der Deklaration 'e1'

        var fix = NavRenameService.GetRenameFix(unit, caret, Settings);
        Assert.That(fix, Is.Not.Null);

        var actual = ApplyChanges(M.Source, fix, "e2");

        Assert.That(actual, Does.Contain("exit e2;"));
        Assert.That(actual, Does.Contain("I1 --> e2;"));
        Assert.That(actual, Does.Not.Contain("e1"));
    }

    [Test]
    public void Rename_FromReference_YieldsSameResult() {

        var unit  = ParseModel(M.Source, @"n:\av\a.nav");
        var caret = M.Position("ref"); // auf der Referenz 'e1'

        var fix = NavRenameService.GetRenameFix(unit, caret, Settings);
        Assert.That(fix, Is.Not.Null);

        var actual = ApplyChanges(M.Source, fix, "e2");

        Assert.That(actual, Does.Contain("exit e2;"));
        Assert.That(actual, Does.Contain("I1 --> e2;"));
    }

    [Test]
    public void Rename_OnWhitespace_ReturnsNull() {

        var unit  = ParseModel(M.Source, @"n:\av\a.nav");
        var caret = M.Position("ws"); // Leerzeile

        var fix = NavRenameService.GetRenameFix(unit, caret, Settings);

        Assert.That(fix, Is.Null);
    }

    [Test]
    public void Rename_OnKeyword_ReturnsNull() {

        var unit  = ParseModel(M.Source, @"n:\av\a.nav");
        var caret = M.Position("kw"); // im Keyword 'exit'

        var fix = NavRenameService.GetRenameFix(unit, caret, Settings);

        Assert.That(fix, Is.Null);
    }

    [Test]
    public void Validate_RejectsAlreadyDeclaredName() {

        var unit  = ParseModel(M.Source, @"n:\av\a.nav");
        var caret = M.Position("decl");

        var fix = NavRenameService.GetRenameFix(unit, caret, Settings);
        Assert.That(fix, Is.Not.Null);

        // 'I1' ist im selben Task bereits als init-Knoten vergeben → abgelehnt.
        Assert.That(fix.ValidateSymbolName("I1"), Is.Not.Null.And.Not.Empty);
        // Ein freier Name ist OK.
        Assert.That(fix.ValidateSymbolName("e2"), Is.Null.Or.Empty);
    }

    [Test]
    public void Rename_ToSameName_ProducesNoChanges() {

        var unit  = ParseModel(M.Source, @"n:\av\a.nav");
        var caret = M.Position("decl");

        var fix = NavRenameService.GetRenameFix(unit, caret, Settings);
        Assert.That(fix, Is.Not.Null);

        var changes = fix.GetTextChanges("e1").Where(c => !c.IsEmpty).ToList();

        Assert.That(changes, Is.Empty);
    }

    #region Helpers

    static string ApplyChanges(string text, Pharmatechnik.Nav.Language.CodeFixes.Refactoring.RenameCodeFix fix, string newName) {
        var writer = new TextChangeWriter();
        return writer.ApplyTextChanges(text, fix.GetTextChanges(newName));
    }

    static CodeGenerationUnit ParseModel(string source, string filePath) {
        var syntax = Syntax.ParseCodeGenerationUnit(text: source, filePath: filePath);
        return CodeGenerationUnit.FromCodeGenerationUnitSyntax(syntax);
    }

    #endregion

}
