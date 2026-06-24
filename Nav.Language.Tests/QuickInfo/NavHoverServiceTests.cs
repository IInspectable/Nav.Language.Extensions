#region Using Directives

using System;
using System.Linq;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.QuickInfo;

#endregion

namespace Nav.Language.Tests.QuickInfo;

[TestFixture]
public class NavHoverServiceTests {

    const string Nav = "task A\n"         +
                       "{\n"              +
                       "    init I1;\n"   +
                       "    exit e1;\n"   +
                       "\n"               + // Leerzeile — garantiert symbolfreie Position
                       "    I1 --> e1;\n" +
                       "}\n";

    [Test]
    public void Hover_OnTaskDefinition_ShowsTaskSignature() {

        var unit  = ParseModel(Nav, @"n:\av\a.nav");
        var caret = IndexOfToken(Nav, "task A", "task "); // auf dem Task-Namen 'A'

        var info = NavHoverService.GetHover(unit, caret);

        Assert.That(info, Is.Not.Null);
        Assert.That(Signature(info), Is.EqualTo("task A"));
    }

    [Test]
    public void Hover_OnExitDeclaration_ShowsExitSignature() {

        var unit  = ParseModel(Nav, @"n:\av\a.nav");
        var caret = IndexOfToken(Nav, "exit e1", "exit "); // auf der Deklaration 'e1'

        var info = NavHoverService.GetHover(unit, caret);

        Assert.That(info, Is.Not.Null);
        Assert.That(Signature(info), Is.EqualTo("exit A:e1"));
    }

    [Test]
    public void Hover_OnNodeReference_ResolvesToDeclaration() {

        var unit  = ParseModel(Nav, @"n:\av\a.nav");
        var caret = IndexOfToken(Nav, "--> e1", "--> "); // auf der Referenz 'e1'

        var info = NavHoverService.GetHover(unit, caret);

        Assert.That(info, Is.Not.Null);
        // Die Referenz löst auf die exit-Deklaration auf (DisplayPartsBuilder folgt der Declaration).
        Assert.That(Signature(info), Is.EqualTo("exit A:e1"));
    }

    [Test]
    public void Hover_CarriesSymbolLocation() {

        var unit  = ParseModel(Nav, @"n:\av\a.nav");
        var caret = IndexOfToken(Nav, "task A", "task ");

        var info = NavHoverService.GetHover(unit, caret);

        Assert.That(info, Is.Not.Null);
        Assert.That(info.Location, Is.Not.Null);
        Assert.That(info.Location.Start, Is.EqualTo(IndexOfToken(Nav, "task A", "task ")));
    }

    [Test]
    public void Hover_OnWhitespace_ReturnsNull() {

        var unit  = ParseModel(Nav, @"n:\av\a.nav");
        var caret = Nav.IndexOf("e1;\n\n", StringComparison.Ordinal) + "e1;\n".Length; // Leerzeile

        var info = NavHoverService.GetHover(unit, caret);

        Assert.That(info, Is.Null);
    }

    #region Helpers

    static string Signature(NavHoverInfo info) {
        return string.Concat(info.DisplayParts.Select(p => p.Text));
    }

    static int IndexOfToken(string source, string anchor, string leading) {
        var anchorIndex = source.IndexOf(anchor, StringComparison.Ordinal);
        Assert.That(anchorIndex, Is.GreaterThanOrEqualTo(0), $"Anker '{anchor}' nicht gefunden.");
        return anchorIndex + leading.Length;
    }

    static CodeGenerationUnit ParseModel(string source, string filePath) {
        var syntax = Syntax.ParseCodeGenerationUnit(text: source, filePath: filePath);
        return CodeGenerationUnit.FromCodeGenerationUnitSyntax(syntax);
    }

    #endregion

}
