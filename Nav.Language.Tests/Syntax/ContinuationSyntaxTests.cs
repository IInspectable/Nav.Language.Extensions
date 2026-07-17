#region Using Directives

using System.Linq;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;

#endregion

namespace Nav.Language.Tests;

/// <summary>
/// Syntax-Tests für die ab Sprachversion 2 gültigen Konstrukte: die Continuation-Kanten (<c>o-^</c>/<c>--^</c>)
/// an einer Transition und die <c>[params …]</c>-Klausel an einer <c>choice</c>-Deklaration. Geprüft wird
/// allein die Baum-Struktur — die Versions-Wirksamkeit (Nav5000) und die Continuation-Semantik sind
/// spätere Schritte.
/// </summary>
[TestFixture]
public class ContinuationSyntaxTests {

    [Test]
    public void ModalContinuation_AttachesTargetTask() {

        var transition = Syntax.ParseTransitionDefinition("Home --> Home o-^ Msg;");

        Assert.That(transition.SyntaxTree.Diagnostics,                             Is.Empty);
        Assert.That(transition.ContinuationTransition,                  Is.Not.Null);
        Assert.That(transition.ContinuationTransition!.Edge,            Is.InstanceOf<ContinuationModalEdgeSyntax>());
        Assert.That(transition.ContinuationTransition.Edge!.Mode,       Is.EqualTo(EdgeMode.Modal));
        Assert.That(transition.ContinuationTransition.Edge.Keyword.Type, Is.EqualTo(SyntaxTokenType.ContinuationModalEdgeKeyword));
        Assert.That(transition.ContinuationTransition.TargetNode?.Name, Is.EqualTo("Msg"));
    }

    [Test]
    public void GotoContinuation_AttachesTargetTask() {

        var transition = Syntax.ParseTransitionDefinition("Home --> Home --^ Drill on OnDrill;");

        Assert.That(transition.SyntaxTree.Diagnostics,                              Is.Empty);
        Assert.That(transition.ContinuationTransition!.Edge,             Is.InstanceOf<ContinuationGoToEdgeSyntax>());
        Assert.That(transition.ContinuationTransition.Edge!.Mode,        Is.EqualTo(EdgeMode.Goto));
        Assert.That(transition.ContinuationTransition.Edge.Keyword.Type, Is.EqualTo(SyntaxTokenType.ContinuationGoToEdgeKeyword));
        Assert.That(transition.ContinuationTransition.TargetNode?.Name,  Is.EqualTo("Drill"));
        // Der Trigger folgt weiterhin hinter der Continuation.
        Assert.That(transition.Trigger, Is.Not.Null);
    }

    [Test]
    public void PlainTransition_HasNoContinuation() {

        var transition = Syntax.ParseTransitionDefinition("Home --> Msg;");

        Assert.That(transition.SyntaxTree.Diagnostics,            Is.Empty);
        Assert.That(transition.ContinuationTransition, Is.Null);
    }

    [Test]
    public void Continuation_OnExitTransition() {

        var exit = Syntax.ParseExitTransitionDefinition("B:Exit --> Home o-^ C;");

        Assert.That(exit.SyntaxTree.Diagnostics,                             Is.Empty);
        Assert.That(exit.ContinuationTransition!.Edge,            Is.InstanceOf<ContinuationModalEdgeSyntax>());
        Assert.That(exit.ContinuationTransition.TargetNode?.Name, Is.EqualTo("C"));
    }

    [Test]
    public void Continuation_WithoutTarget_ReportsMissingTargetNode() {

        var transition = Syntax.ParseTransitionDefinition("Home --> Home o-^ ;");

        Assert.That(transition.ContinuationTransition,            Is.Not.Null);
        Assert.That(transition.ContinuationTransition!.TargetNode, Is.Null);
        Assert.That(transition.SyntaxTree.Diagnostics.Select(d => d.Message), Has.Some.Contains("missing target node"));
    }

    [Test]
    public void ChoiceDeclaration_WithParams() {

        var choice = Syntax.ParseChoiceNodeDeclaration("choice Choice_Retry [params string reason, int level];");

        Assert.That(choice.SyntaxTree.Diagnostics,                          Is.Empty);
        Assert.That(choice.Identifier.ToString(),                Is.EqualTo("Choice_Retry"));
        Assert.That(choice.CodeParamsDeclaration,                Is.Not.Null);
        Assert.That(choice.CodeParamsDeclaration!.ParameterList, Is.Not.Null);
        Assert.That(choice.CodeParamsDeclaration.ParameterList!.Count, Is.EqualTo(2));
        Assert.That(choice.CodeParamsDeclaration.ParameterList[0].Identifier.ToString(), Is.EqualTo("reason"));
        Assert.That(choice.CodeParamsDeclaration.ParameterList[1].Identifier.ToString(), Is.EqualTo("level"));
    }

    [Test]
    public void ChoiceDeclaration_WithoutParams_HasNone() {

        var choice = Syntax.ParseChoiceNodeDeclaration("choice Choice_Retry;");

        Assert.That(choice.SyntaxTree.Diagnostics,           Is.Empty);
        Assert.That(choice.CodeParamsDeclaration, Is.Null);
    }

}
