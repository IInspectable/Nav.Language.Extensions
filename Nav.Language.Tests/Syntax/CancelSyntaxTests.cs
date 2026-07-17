#region Using Directives

using NUnit.Framework;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Nav.Language.Tests;

/// <summary>
/// Syntax-Tests für das ab Sprachversion 2 vorgesehene <c>cancel</c>-Kantenziel (<see cref="CancelTargetNodeSyntax"/>).
/// Geprüft wird allein die Baum-Struktur und die Keyword-Klassifikation — die Versions-Wirksamkeit (Nav5xxx),
/// die Stellen-Restriktion und die Cancel-Semantik sind spätere Schritte. Anders als <c>end</c> hat
/// <c>cancel</c> keine Knoten-Deklaration; es existiert nur als Ziel hinter einer Kante.
/// </summary>
[TestFixture]
public class CancelSyntaxTests {

    [Test]
    public void CancelTarget_OnGoToTransition() {

        var transition = Syntax.ParseTransitionDefinition("View --> cancel;");

        Assert.That(transition.SyntaxTree.Diagnostics, Is.Empty);
        Assert.That(transition.TargetNode,             Is.InstanceOf<CancelTargetNodeSyntax>());

        var target = (CancelTargetNodeSyntax) transition.TargetNode!;
        Assert.That(target.Name,                    Is.EqualTo("cancel"));
        Assert.That(target.CancelKeyword.Type,      Is.EqualTo(SyntaxTokenType.CancelKeyword));
        Assert.That(target.CancelKeyword.IsMissing, Is.False);
    }

    [Test]
    public void CancelTarget_IsClassifiedAsKeyword() {

        var transition = Syntax.ParseTransitionDefinition("View --> cancel;");
        var target     = (CancelTargetNodeSyntax) transition.TargetNode!;

        Assert.That(target.CancelKeyword.Classification, Is.EqualTo(TextClassification.Keyword));
    }

    [Test]
    public void CancelTarget_OnChoiceArm_WithCondition() {

        // Der bedingte Cancel-Ausgang an einem Choice-Arm: `Choice --> cancel if "…";`.
        var transition = Syntax.ParseTransitionDefinition("""C --> cancel if "Abbruch";""");

        Assert.That(transition.SyntaxTree.Diagnostics, Is.Empty);
        Assert.That(transition.TargetNode,             Is.InstanceOf<CancelTargetNodeSyntax>());
        Assert.That(transition.ConditionClause,        Is.InstanceOf<IfConditionClauseSyntax>());
    }

    [Test]
    public void CancelTarget_OnDirectTriggerEdge() {

        // Der unbedingte Swallow an einer direkten Trigger-Kante: `View --> cancel on OnEscape;`.
        var transition = Syntax.ParseTransitionDefinition("View --> cancel on OnEscape;");

        Assert.That(transition.SyntaxTree.Diagnostics, Is.Empty);
        Assert.That(transition.TargetNode,             Is.InstanceOf<CancelTargetNodeSyntax>());
        Assert.That(transition.Trigger,                Is.InstanceOf<SignalTriggerSyntax>());
    }

    [Test]
    public void CancelTargetNode_Snippet_ParsesWithoutDiagnostics() {

        var node = Syntax.ParseCancelTargetNode("cancel");

        Assert.That(node.SyntaxTree.Diagnostics, Is.Empty);
        Assert.That(node.Name,                   Is.EqualTo("cancel"));
        Assert.That(node.CancelKeyword.Type,     Is.EqualTo(SyntaxTokenType.CancelKeyword));
    }

    [Test]
    public void Cancel_IsReservedKeyword() {

        Assert.That(SyntaxFacts.IsKeyword(SyntaxFacts.CancelKeyword),         Is.True);
        Assert.That(SyntaxFacts.IsNavKeyword(SyntaxFacts.CancelKeyword),      Is.True);
        Assert.That(SyntaxFacts.IsValidIdentifier(SyntaxFacts.CancelKeyword), Is.False);
    }

}
