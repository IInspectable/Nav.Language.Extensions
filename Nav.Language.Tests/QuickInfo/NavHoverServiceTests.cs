#region Using Directives

using System.Linq;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.QuickInfo;

#endregion

namespace Nav.Language.Tests.QuickInfo;

[TestFixture]
public class NavHoverServiceTests {

    //   |taskA:A|  Task-Name 'A'
    //   |decl:e1|  exit-Deklaration 'e1'
    //   |ws:|      Leerzeile — garantiert symbolfreie Position
    //   |ref:e1|   Referenz auf 'e1'
    static readonly NavMarkup M = NavMarkup.Parse(
        """
        task |taskA:A|
        {
            init I1;
            exit |decl:e1|;
        |ws:|
            I1 --> |ref:e1|;
        }

        """);

    [Test]
    public void Hover_OnTaskDefinition_ShowsTaskSignature() {

        var unit  = ParseModel(M.Source, @"n:\av\a.nav");
        var caret = M.Position("taskA"); // auf dem Task-Namen 'A'

        var info = NavHoverService.GetHover(unit, caret);

        Assert.That(info, Is.Not.Null);
        Assert.That(Signature(info), Is.EqualTo("task A"));
    }

    [Test]
    public void Hover_OnExitDeclaration_ShowsExitSignature() {

        var unit  = ParseModel(M.Source, @"n:\av\a.nav");
        var caret = M.Position("decl"); // auf der Deklaration 'e1'

        var info = NavHoverService.GetHover(unit, caret);

        Assert.That(info, Is.Not.Null);
        Assert.That(Signature(info), Is.EqualTo("exit A:e1"));
    }

    [Test]
    public void Hover_OnNodeReference_ResolvesToDeclaration() {

        var unit  = ParseModel(M.Source, @"n:\av\a.nav");
        var caret = M.Position("ref"); // auf der Referenz 'e1'

        var info = NavHoverService.GetHover(unit, caret);

        Assert.That(info, Is.Not.Null);
        // Die Referenz löst auf die exit-Deklaration auf (DisplayPartsBuilder folgt der Declaration).
        Assert.That(Signature(info), Is.EqualTo("exit A:e1"));
    }

    [Test]
    public void Hover_CarriesSymbolLocation() {

        var unit  = ParseModel(M.Source, @"n:\av\a.nav");
        var caret = M.Position("taskA");

        var info = NavHoverService.GetHover(unit, caret);

        Assert.That(info, Is.Not.Null);
        Assert.That(info.Location, Is.Not.Null);
        Assert.That(info.Location.Start, Is.EqualTo(M.Position("taskA")));
    }

    //   |choice:C1|  Choice-Name 'C1'
    //   |edge:|      Pfeil-Token '-->' der Kante zur Choice
    static readonly NavMarkup ChoiceM = NavMarkup.Parse(
        """
        task A
        {
            init I1;
            exit e1;
            I1 --> e1;
        }
        task B
        {
            init I1;
            exit e1;
            I1 --> e1;
        }
        task C
        {
            init I1;
            task A;
            task B;
            exit e1;
            choice |choice:C1|;

            I1   |edge:|--> C1;
            C1   --> A;
            C1   --> B;
            A:e1 --> e1;
            B:e1 --> e1;
        }

        """);

    [Test]
    public void Hover_OnChoiceNode_ListsAllReachableNodes() {

        var unit  = ParseModel(ChoiceM.Source, @"n:\av\c.nav");
        var caret = ChoiceM.Position("choice"); // auf dem Choice-Namen 'C1'

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

        var unit  = ParseModel(ChoiceM.Source, @"n:\av\c.nav");
        var caret = ChoiceM.Position("edge"); // auf dem Pfeil zur Choice

        var info = NavHoverService.GetHover(unit, caret);

        Assert.That(info, Is.Not.Null);
        // Edge-Mode trägt keine eigene Signatur — nur die durch die Choice erreichbaren Knoten.
        Assert.That(Signature(info), Is.Empty);
        Assert.That(info.Calls.Select(c => c.Node.Name).ToList(), Is.EquivalentTo(new[] { "A", "B" }));
    }

    [Test]
    public void Hover_OnWhitespace_ReturnsNull() {

        var unit  = ParseModel(M.Source, @"n:\av\a.nav");
        var caret = M.Position("ws"); // Leerzeile

        var info = NavHoverService.GetHover(unit, caret);

        Assert.That(info, Is.Null);
    }

    [Test]
    public void Hover_WithCommentAboveTask_CapturesDocumentation() {

        var m = NavMarkup.Parse(
            """
            // Beschreibt die Aufgabe A
            task |A
            {
                init I1;
                exit e1;
                I1 --> e1;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\a.nav");
        var caret = m.Caret;

        var info = NavHoverService.GetHover(unit, caret);

        Assert.That(info,               Is.Not.Null);
        Assert.That(info.Documentation, Is.EqualTo("Beschreibt die Aufgabe A"));
    }

    [Test]
    public void Hover_WithIndentedCommentAboveNode_CapturesDocumentation() {

        var m = NavMarkup.Parse(
            """
            task A
            {
                init I1;
                // Der Ausgang
                exit |e1;
                I1 --> e1;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\a.nav");
        var caret = m.Caret;

        var info = NavHoverService.GetHover(unit, caret);

        Assert.That(info,               Is.Not.Null);
        Assert.That(info.Documentation, Is.EqualTo("Der Ausgang"));
    }

    [Test]
    public void Hover_OnNodeReference_CapturesDeclarationDocumentation() {

        var m = NavMarkup.Parse(
            """
            task A
            {
                init I1;
                // Der Ausgang
                exit e1;
                I1 --> |e1;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\a.nav");
        var caret = m.Caret; // auf der Referenz 'e1'

        var info = NavHoverService.GetHover(unit, caret);

        Assert.That(info,               Is.Not.Null);
        // Die Referenz erbt die Doku ihrer Deklaration.
        Assert.That(info.Documentation, Is.EqualTo("Der Ausgang"));
    }

    [Test]
    public void Hover_WithBlankLineBetweenCommentAndNode_HasNoDocumentation() {

        // Leerzeile trennt den Fernkommentar von 'task A' ab.
        var m = NavMarkup.Parse(
            """
            // Fernkommentar

            task |A
            {
                init I1;
                exit e1;
                I1 --> e1;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\a.nav");
        var caret = m.Caret;

        var info = NavHoverService.GetHover(unit, caret);

        Assert.That(info,               Is.Not.Null);
        Assert.That(info.Documentation, Is.Null);
    }

    [Test]
    public void Hover_WithMultiLineCommentAboveTask_CapturesEachLine() {

        var m = NavMarkup.Parse(
            """
            /* Zeile 1
               Zeile 2 */
            task |A
            {
                init I1;
                exit e1;
                I1 --> e1;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\a.nav");
        var caret = m.Caret;

        var info = NavHoverService.GetHover(unit, caret);

        Assert.That(info,               Is.Not.Null);
        Assert.That(info.Documentation, Is.EqualTo("Zeile 1\nZeile 2"));
    }

    [Test]
    public void Hover_WithOnlyAdjacentCommentBlock_IgnoresEarlierBlock() {

        var m = NavMarkup.Parse(
            """
            // weit oben

            // direkt darüber
            task |A
            {
                init I1;
                exit e1;
                I1 --> e1;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\a.nav");
        var caret = m.Caret;

        var info = NavHoverService.GetHover(unit, caret);

        Assert.That(info,               Is.Not.Null);
        Assert.That(info.Documentation, Is.EqualTo("direkt darüber"));
    }

    // Task A trägt eine Doku; Task C verwendet A als Task-Knoten.
    //   |node:A|  Task-Knoten 'A' in C
    static readonly NavMarkup TaskNodeDocM = NavMarkup.Parse(
        """
        // Doku der Task A
        task A
        {
            init I1;
            exit e1;
            I1 --> e1;
        }
        task C
        {
            init I1;
            task |node:A|;
            exit e1;

            I1   --> A;
            A:e1 --> e1;
        }

        """);

    [Test]
    public void Hover_OnTaskNode_ShowsTaskDefinitionDocumentation() {

        var unit  = ParseModel(TaskNodeDocM.Source, @"n:\av\c.nav");
        var caret = TaskNodeDocM.Position("node"); // auf dem Task-Knoten 'A' in C

        var info = NavHoverService.GetHover(unit, caret);

        Assert.That(info,               Is.Not.Null);
        // Die Signatur zeigt die Task, also zeigt auch die Doku den Kommentar der Task-Definition.
        Assert.That(Signature(info),    Is.EqualTo("task A"));
        Assert.That(info.Documentation, Is.EqualTo("Doku der Task A"));
    }

    [Test]
    public void Hover_OnTaskNode_IgnoresCallSiteComment() {

        // Kommentar nur über der Verwendung 'task A;', nicht über der Definition.
        var m = NavMarkup.Parse(
            """
            task A
            {
                init I1;
                exit e1;
                I1 --> e1;
            }
            task C
            {
                init I1;
                // Aufrufstellen-Notiz
                task |A;
                exit e1;

                I1   --> A;
                A:e1 --> e1;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\c.nav");
        var caret = m.Caret;

        var info = NavHoverService.GetHover(unit, caret);

        Assert.That(info,               Is.Not.Null);
        // Die Aufrufstellen-Notiz wird bewusst nicht der Task zugeordnet.
        Assert.That(info.Documentation, Is.Null);
    }

    [Test]
    public void Hover_OnTaskNodeAlias_ShowsTaskDefinitionDocumentation() {

        // Alias-Syntax ist 'task Identifier Alias;' (ohne 'as').
        var m = NavMarkup.Parse(
            """
            // Doku der Task A
            task A
            {
                init I1;
                exit e1;
                I1 --> e1;
            }
            task C
            {
                init I1;
                task A |Foo;
                exit e1;

                I1     --> Foo;
                Foo:e1 --> e1;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\c.nav");
        var caret = m.Caret; // auf dem Alias 'Foo'

        var info = NavHoverService.GetHover(unit, caret);

        Assert.That(info,               Is.Not.Null);
        Assert.That(info.Documentation, Is.EqualTo("Doku der Task A"));
    }

    #region Helpers

    static string Signature(NavHoverInfo info) {
        return string.Concat(info.DisplayParts.Select(p => p.Text));
    }

    static CodeGenerationUnit ParseModel(string source, string filePath) {
        var syntax = Syntax.ParseCodeGenerationUnit(text: source, filePath: filePath);
        return CodeGenerationUnit.FromCodeGenerationUnitSyntax(syntax);
    }

    #endregion

}
