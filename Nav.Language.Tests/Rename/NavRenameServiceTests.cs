#region Using Directives

using System;
using System.Linq;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.Rename;
using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Nav.Language.Tests.Rename;

[TestFixture]
public class NavRenameServiceTests {

    const string Nav = "task A\n"         +
                       "{\n"              +
                       "    init I1;\n"   +
                       "    exit e1;\n"   +
                       "\n"               + // Leerzeile — garantiert symbolfreie Position
                       "    I1 --> e1;\n" +
                       "}\n";

    static readonly TextEditorSettings Settings = new(tabSize: 4, newLine: "\n");

    [Test]
    public void Rename_FromDeclaration_RenamesDeclarationAndReference() {

        var unit  = ParseModel(Nav, @"n:\av\a.nav");
        var caret = IndexOfToken(Nav, "exit e1", "exit "); // auf der Deklaration 'e1'

        var fix = NavRenameService.GetRenameFix(unit, caret, Settings);
        Assert.That(fix, Is.Not.Null);

        var actual = ApplyChanges(Nav, fix, "e2");

        Assert.That(actual, Does.Contain("exit e2;"));
        Assert.That(actual, Does.Contain("I1 --> e2;"));
        Assert.That(actual, Does.Not.Contain("e1"));
    }

    [Test]
    public void Rename_FromReference_YieldsSameResult() {

        var unit  = ParseModel(Nav, @"n:\av\a.nav");
        var caret = IndexOfToken(Nav, "--> e1", "--> "); // auf der Referenz 'e1'

        var fix = NavRenameService.GetRenameFix(unit, caret, Settings);
        Assert.That(fix, Is.Not.Null);

        var actual = ApplyChanges(Nav, fix, "e2");

        Assert.That(actual, Does.Contain("exit e2;"));
        Assert.That(actual, Does.Contain("I1 --> e2;"));
    }

    [Test]
    public void Rename_OnWhitespace_ReturnsNull() {

        var unit  = ParseModel(Nav, @"n:\av\a.nav");
        var caret = Nav.IndexOf("e1;\n\n", StringComparison.Ordinal) + "e1;\n".Length; // Leerzeile

        var fix = NavRenameService.GetRenameFix(unit, caret, Settings);

        Assert.That(fix, Is.Null);
    }

    [Test]
    public void Rename_OnKeyword_ReturnsNull() {

        var unit  = ParseModel(Nav, @"n:\av\a.nav");
        var caret = Nav.IndexOf("exit", StringComparison.Ordinal) + 1; // im Keyword 'exit'

        var fix = NavRenameService.GetRenameFix(unit, caret, Settings);

        Assert.That(fix, Is.Null);
    }

    [Test]
    public void Validate_RejectsAlreadyDeclaredName() {

        var unit  = ParseModel(Nav, @"n:\av\a.nav");
        var caret = IndexOfToken(Nav, "exit e1", "exit ");

        var fix = NavRenameService.GetRenameFix(unit, caret, Settings);
        Assert.That(fix, Is.Not.Null);

        // 'I1' ist im selben Task bereits als init-Knoten vergeben → abgelehnt.
        Assert.That(fix.ValidateSymbolName("I1"), Is.Not.Null.And.Not.Empty);
        // Ein freier Name ist OK.
        Assert.That(fix.ValidateSymbolName("e2"), Is.Null.Or.Empty);
    }

    [Test]
    public void Rename_ToSameName_ProducesNoChanges() {

        var unit  = ParseModel(Nav, @"n:\av\a.nav");
        var caret = IndexOfToken(Nav, "exit e1", "exit ");

        var fix = NavRenameService.GetRenameFix(unit, caret, Settings);
        Assert.That(fix, Is.Not.Null);

        var changes = fix.GetTextChanges("e1").Where(c => !c.IsEmpty).ToList();

        Assert.That(changes, Is.Empty);
    }

    #region Helpers

    static int IndexOfToken(string source, string anchor, string leading) {
        var anchorIndex = source.IndexOf(anchor, StringComparison.Ordinal);
        Assert.That(anchorIndex, Is.GreaterThanOrEqualTo(0), $"Anker '{anchor}' nicht gefunden.");
        return anchorIndex + leading.Length;
    }

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
