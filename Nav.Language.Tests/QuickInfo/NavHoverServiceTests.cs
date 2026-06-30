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

    [Test]
    public void Hover_WithCommentAboveTask_CapturesDocumentation() {

        const string nav = "// Beschreibt die Aufgabe A\n" +
                           "task A\n"                        +
                           "{\n"                             +
                           "    init I1;\n"                  +
                           "    exit e1;\n"                  +
                           "    I1 --> e1;\n"                +
                           "}\n";

        var unit  = ParseModel(nav, @"n:\av\a.nav");
        var caret = IndexOfToken(nav, "task A", "task ");

        var info = NavHoverService.GetHover(unit, caret);

        Assert.That(info,               Is.Not.Null);
        Assert.That(info.Documentation, Is.EqualTo("Beschreibt die Aufgabe A"));
    }

    [Test]
    public void Hover_WithIndentedCommentAboveNode_CapturesDocumentation() {

        const string nav = "task A\n"               +
                           "{\n"                     +
                           "    init I1;\n"          +
                           "    // Der Ausgang\n"    +
                           "    exit e1;\n"          +
                           "    I1 --> e1;\n"        +
                           "}\n";

        var unit  = ParseModel(nav, @"n:\av\a.nav");
        var caret = IndexOfToken(nav, "exit e1", "exit ");

        var info = NavHoverService.GetHover(unit, caret);

        Assert.That(info,               Is.Not.Null);
        Assert.That(info.Documentation, Is.EqualTo("Der Ausgang"));
    }

    [Test]
    public void Hover_OnNodeReference_CapturesDeclarationDocumentation() {

        const string nav = "task A\n"               +
                           "{\n"                     +
                           "    init I1;\n"          +
                           "    // Der Ausgang\n"    +
                           "    exit e1;\n"          +
                           "    I1 --> e1;\n"        +
                           "}\n";

        var unit  = ParseModel(nav, @"n:\av\a.nav");
        var caret = IndexOfToken(nav, "--> e1", "--> "); // auf der Referenz 'e1'

        var info = NavHoverService.GetHover(unit, caret);

        Assert.That(info,               Is.Not.Null);
        // Die Referenz erbt die Doku ihrer Deklaration.
        Assert.That(info.Documentation, Is.EqualTo("Der Ausgang"));
    }

    [Test]
    public void Hover_WithBlankLineBetweenCommentAndNode_HasNoDocumentation() {

        const string nav = "// Fernkommentar\n" +
                           "\n"                  + // Leerzeile trennt ab
                           "task A\n"            +
                           "{\n"                 +
                           "    init I1;\n"      +
                           "    exit e1;\n"      +
                           "    I1 --> e1;\n"    +
                           "}\n";

        var unit  = ParseModel(nav, @"n:\av\a.nav");
        var caret = IndexOfToken(nav, "task A", "task ");

        var info = NavHoverService.GetHover(unit, caret);

        Assert.That(info,               Is.Not.Null);
        Assert.That(info.Documentation, Is.Null);
    }

    [Test]
    public void Hover_WithMultiLineCommentAboveTask_CapturesEachLine() {

        const string nav = "/* Zeile 1\n"   +
                           "   Zeile 2 */\n" +
                           "task A\n"        +
                           "{\n"             +
                           "    init I1;\n"  +
                           "    exit e1;\n"  +
                           "    I1 --> e1;\n" +
                           "}\n";

        var unit  = ParseModel(nav, @"n:\av\a.nav");
        var caret = IndexOfToken(nav, "task A", "task ");

        var info = NavHoverService.GetHover(unit, caret);

        Assert.That(info,               Is.Not.Null);
        Assert.That(info.Documentation, Is.EqualTo("Zeile 1\nZeile 2"));
    }

    [Test]
    public void Hover_WithOnlyAdjacentCommentBlock_IgnoresEarlierBlock() {

        const string nav = "// weit oben\n"        +
                           "\n"                     +
                           "// direkt darüber\n"    +
                           "task A\n"               +
                           "{\n"                    +
                           "    init I1;\n"         +
                           "    exit e1;\n"         +
                           "    I1 --> e1;\n"       +
                           "}\n";

        var unit  = ParseModel(nav, @"n:\av\a.nav");
        var caret = IndexOfToken(nav, "task A", "task ");

        var info = NavHoverService.GetHover(unit, caret);

        Assert.That(info,               Is.Not.Null);
        Assert.That(info.Documentation, Is.EqualTo("direkt darüber"));
    }

    // Task A trägt eine Doku; Task C verwendet A als Task-Knoten.
    const string TaskNodeDocNav = "// Doku der Task A\n" +
                                  "task A\n"             +
                                  "{\n"                  +
                                  "    init I1;\n"       +
                                  "    exit e1;\n"       +
                                  "    I1 --> e1;\n"     +
                                  "}\n"                  +
                                  "task C\n"             +
                                  "{\n"                  +
                                  "    init I1;\n"       +
                                  "    task A;\n"        +
                                  "    exit e1;\n"       +
                                  "\n"                   +
                                  "    I1   --> A;\n"    +
                                  "    A:e1 --> e1;\n"   +
                                  "}\n";

    [Test]
    public void Hover_OnTaskNode_ShowsTaskDefinitionDocumentation() {

        var unit  = ParseModel(TaskNodeDocNav, @"n:\av\c.nav");
        var caret = IndexOfToken(TaskNodeDocNav, "task A;", "task "); // auf dem Task-Knoten 'A' in C

        var info = NavHoverService.GetHover(unit, caret);

        Assert.That(info,               Is.Not.Null);
        // Die Signatur zeigt die Task, also zeigt auch die Doku den Kommentar der Task-Definition.
        Assert.That(Signature(info),    Is.EqualTo("task A"));
        Assert.That(info.Documentation, Is.EqualTo("Doku der Task A"));
    }

    [Test]
    public void Hover_OnTaskNode_IgnoresCallSiteComment() {

        // Kommentar nur über der Verwendung 'task A;', nicht über der Definition.
        const string nav = "task A\n"                       +
                           "{\n"                             +
                           "    init I1;\n"                  +
                           "    exit e1;\n"                  +
                           "    I1 --> e1;\n"                +
                           "}\n"                             +
                           "task C\n"                        +
                           "{\n"                             +
                           "    init I1;\n"                  +
                           "    // Aufrufstellen-Notiz\n"    +
                           "    task A;\n"                   +
                           "    exit e1;\n"                  +
                           "\n"                              +
                           "    I1   --> A;\n"               +
                           "    A:e1 --> e1;\n"              +
                           "}\n";

        var unit  = ParseModel(nav, @"n:\av\c.nav");
        var caret = IndexOfToken(nav, "task A;", "task ");

        var info = NavHoverService.GetHover(unit, caret);

        Assert.That(info,               Is.Not.Null);
        // Die Aufrufstellen-Notiz wird bewusst nicht der Task zugeordnet.
        Assert.That(info.Documentation, Is.Null);
    }

    [Test]
    public void Hover_OnTaskNodeAlias_ShowsTaskDefinitionDocumentation() {

        // Alias-Syntax ist 'task Identifier Alias;' (ohne 'as').
        const string nav = "// Doku der Task A\n"  +
                           "task A\n"              +
                           "{\n"                   +
                           "    init I1;\n"        +
                           "    exit e1;\n"        +
                           "    I1 --> e1;\n"      +
                           "}\n"                   +
                           "task C\n"              +
                           "{\n"                   +
                           "    init I1;\n"        +
                           "    task A Foo;\n"     +
                           "    exit e1;\n"        +
                           "\n"                    +
                           "    I1     --> Foo;\n" +
                           "    Foo:e1 --> e1;\n"  +
                           "}\n";

        var unit  = ParseModel(nav, @"n:\av\c.nav");
        var caret = IndexOfToken(nav, "task A Foo", "task A "); // auf dem Alias 'Foo'

        var info = NavHoverService.GetHover(unit, caret);

        Assert.That(info,               Is.Not.Null);
        Assert.That(info.Documentation, Is.EqualTo("Doku der Task A"));
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
