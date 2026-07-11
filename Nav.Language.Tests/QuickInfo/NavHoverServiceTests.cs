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
    public void Hover_OnEdgeToChoice_ShowsEdgeMeaningNotFanOut() {

        var unit  = ParseModel(ChoiceM.Source, @"n:\av\c.nav");
        var caret = ChoiceM.Position("edge"); // auf dem Pfeil zur Choice

        var info = NavHoverService.GetHover(unit, caret);

        Assert.That(info, Is.Not.Null);
        // Auch wenn die Kante auf eine Choice zeigt: sie erklärt ihre eigene Bedeutung, NICHT den Fan-out.
        // Der Fan-out der erreichbaren Knoten bleibt der Choice vorbehalten (Hover_OnChoiceNode_...).
        Assert.That(Signature(info),     Is.EqualTo("GoTo Edge"));
        Assert.That(info.Calls,          Is.Empty);
        Assert.That(info.Documentation,  Is.EqualTo("Ruft das Ziel auf (nicht modal)."));
    }

    //   |goto:|      Pfeil '-->'  (GoTo Edge)
    //   |modal:|     Pfeil 'o->'  (Modal Edge)
    //   |nonmodal:|  Pfeil '==>'  (NonModal Edge)
    //   |cont:|      Pfeil 'o-^'  (Modal Continuation)
    //   |gcont:|     Pfeil '--^'  (GoTo Continuation)
    static readonly NavMarkup EdgeM = NavMarkup.Parse(
        """
        task Sample
        {
            init Init1;
            exit Exit;
            task Msg;
            view View;
            choice Choice_Retry;

            Init1        |goto:|--> Choice_Retry;
            Choice_Retry --> View |cont:|o-^ Msg;
            Msg:Exit     --> View |gcont:|--^ Msg;
            View         |modal:|o-> Msg on OnA;
            View         |nonmodal:|==> Msg on OnB;
        }

        """);

    [Test]
    public void Hover_OnGoToEdge_ShowsMeaning() {

        var unit = ParseModel(EdgeM.Source, @"n:\av\e.nav");
        var info = NavHoverService.GetHover(unit, EdgeM.Position("goto"));

        Assert.That(info,               Is.Not.Null);
        Assert.That(Signature(info),    Is.EqualTo("GoTo Edge"));
        Assert.That(info.Documentation, Is.EqualTo("Ruft das Ziel auf (nicht modal)."));
        Assert.That(info.Calls,         Is.Empty);
    }

    [Test]
    public void Hover_OnModalEdge_ShowsMeaning() {

        var unit = ParseModel(EdgeM.Source, @"n:\av\e.nav");
        var info = NavHoverService.GetHover(unit, EdgeM.Position("modal"));

        Assert.That(info,               Is.Not.Null);
        Assert.That(Signature(info),    Is.EqualTo("Modal Edge"));
        Assert.That(info.Documentation, Is.EqualTo("Ruft das Ziel modal auf."));
        Assert.That(info.Calls,         Is.Empty);
    }

    [Test]
    public void Hover_OnNonModalEdge_ShowsMeaning() {

        var unit = ParseModel(EdgeM.Source, @"n:\av\e.nav");
        var info = NavHoverService.GetHover(unit, EdgeM.Position("nonmodal"));

        Assert.That(info,               Is.Not.Null);
        Assert.That(Signature(info),    Is.EqualTo("NonModal Edge"));
        Assert.That(info.Documentation, Is.EqualTo("Ruft das Ziel nicht-modal auf."));
        Assert.That(info.Calls,         Is.Empty);
    }

    [Test]
    public void Hover_OnModalContinuation_ShowsContinuationMeaning() {

        var unit = ParseModel(EdgeM.Source, @"n:\av\e.nav");
        var info = NavHoverService.GetHover(unit, EdgeM.Position("cont"));

        Assert.That(info,               Is.Not.Null);
        // o-^ ist eine Continuation, KEINE gewöhnliche Modal-Kante — und zeigt nicht mehr den Folge-Task.
        // Die Modalität betrifft den Folge-Task-Aufruf, nicht die Anzeige der GUI (die läuft per Goto).
        Assert.That(Signature(info),    Is.EqualTo("Modal Continuation"));
        Assert.That(info.Documentation, Is.EqualTo("Zeigt die GUI an und ruft unmittelbar den Folge-Task modal auf."));
        Assert.That(info.Calls,         Is.Empty);
    }

    [Test]
    public void Hover_OnGoToContinuation_ShowsContinuationMeaning() {

        var unit = ParseModel(EdgeM.Source, @"n:\av\e.nav");
        var info = NavHoverService.GetHover(unit, EdgeM.Position("gcont"));

        Assert.That(info,               Is.Not.Null);
        Assert.That(Signature(info),    Is.EqualTo("GoTo Continuation"));
        Assert.That(info.Documentation, Is.EqualTo("Zeigt die GUI an und ruft unmittelbar den Folge-Task auf (nicht modal)."));
        Assert.That(info.Calls,         Is.Empty);
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

    //   |on:|  Trigger-Keyword 'on'
    //   |if:|  Bedingungs-Keyword 'if'
    //   |do:|  Handlungs-Keyword 'do'
    static readonly NavMarkup KeywordM = NavMarkup.Parse(
        """
        task A
        {
            init I1;
            dialog D1;
            exit e1;
            I1 --> D1 |on:|on Something |if:|if "c" |do:|do "act";
            D1 --> e1;
        }

        """);

    [Test]
    public void Hover_OnTriggerKeyword_ShowsMeaning() {

        var unit = ParseModel(KeywordM.Source, @"n:\av\k.nav");
        var info = NavHoverService.GetHover(unit, KeywordM.Position("on"));

        Assert.That(info,               Is.Not.Null);
        Assert.That(Signature(info),    Is.EqualTo("on"));
        Assert.That(info.Documentation, Is.EqualTo(SyntaxFacts.GetKeywordDescription(SyntaxFacts.OnKeyword)));
        Assert.That(info.Calls,         Is.Empty);
    }

    [Test]
    public void Hover_OnConditionKeyword_ShowsMeaning() {

        var unit = ParseModel(KeywordM.Source, @"n:\av\k.nav");
        var info = NavHoverService.GetHover(unit, KeywordM.Position("if"));

        Assert.That(info,               Is.Not.Null);
        Assert.That(Signature(info),    Is.EqualTo("if"));
        Assert.That(info.Documentation, Is.EqualTo(SyntaxFacts.GetKeywordDescription(SyntaxFacts.IfKeyword)));
    }

    [Test]
    public void Hover_OnDoKeyword_ShowsMeaning() {

        var unit = ParseModel(KeywordM.Source, @"n:\av\k.nav");
        var info = NavHoverService.GetHover(unit, KeywordM.Position("do"));

        Assert.That(info,               Is.Not.Null);
        Assert.That(Signature(info),    Is.EqualTo("do"));
        Assert.That(info.Documentation, Is.EqualTo(SyntaxFacts.GetKeywordDescription(SyntaxFacts.DoKeyword)));
    }

    [Test]
    public void Hover_OnKeywordCarriesTokenLocation() {

        var unit = ParseModel(KeywordM.Source, @"n:\av\k.nav");
        var info = NavHoverService.GetHover(unit, KeywordM.Position("do"));

        Assert.That(info,          Is.Not.Null);
        Assert.That(info.Location, Is.Not.Null);
        Assert.That(info.Location.Start, Is.EqualTo(KeywordM.Position("do")));
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
