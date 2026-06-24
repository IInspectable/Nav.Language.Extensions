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

    const string ChoiceNav = "task A\n"           +
                             "{\n"                 +
                             "    init I1;\n"      +
                             "    exit e1;\n"      +
                             "    I1 --> e1;\n"    +
                             "}\n"                 +
                             "task B\n"            +
                             "{\n"                 +
                             "    init I1;\n"      +
                             "    exit e1;\n"      +
                             "    I1 --> e1;\n"    +
                             "}\n"                 +
                             "task C\n"            +
                             "{\n"                 +
                             "    init I1;\n"      +
                             "    task A;\n"        +
                             "    task B;\n"        +
                             "    exit e1;\n"       +
                             "    choice C1;\n"     +
                             "\n"                   +
                             "    I1   --> C1;\n"   +
                             "    C1   --> A;\n"    +
                             "    C1   --> B;\n"    +
                             "    A:e1 --> e1;\n"   +
                             "    B:e1 --> e1;\n"   +
                             "}\n";

    [Test]
    public void Hover_OnChoiceNode_ListsAllReachableNodes() {

        var unit  = ParseModel(ChoiceNav, @"n:\av\c.nav");
        var caret = IndexOfToken(ChoiceNav, "choice C1", "choice "); // auf dem Choice-Namen 'C1'

        var info = NavHoverService.GetHover(unit, caret);

        Assert.That(info, Is.Not.Null);
        // Kopfzeile: die Choice-Signatur ...
        Assert.That(Signature(info), Is.EqualTo("choice C1"));
        // ... und beide transitiv erreichbaren Knoten als Calls.
        Assert.That(info.Calls.Select(c => c.Node.Name).ToList(), Is.EquivalentTo(new[] { "A", "B" }));
        // Der Edge-Mode trägt das getippte Pfeil-Token (nicht das ausgeschriebene Verb) — für die Anzeige.
        Assert.That(info.Calls.Select(c => c.EdgeMode.Name).ToList(), Is.All.EqualTo("-->"));
    }

    [Test]
    public void Hover_OnEdgeToChoice_ResolvesReachableNodes() {

        var unit  = ParseModel(ChoiceNav, @"n:\av\c.nav");
        var caret = ChoiceNav.IndexOf("--> C1", StringComparison.Ordinal); // auf dem Pfeil zur Choice

        var info = NavHoverService.GetHover(unit, caret);

        Assert.That(info, Is.Not.Null);
        // Edge-Mode trägt keine eigene Signatur — nur die durch die Choice erreichbaren Knoten.
        Assert.That(Signature(info), Is.Empty);
        Assert.That(info.Calls.Select(c => c.Node.Name).ToList(), Is.EquivalentTo(new[] { "A", "B" }));
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
