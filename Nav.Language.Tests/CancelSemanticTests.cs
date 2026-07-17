using System.Linq;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;

// ReSharper disable PossibleNullReferenceException

namespace Nav.Language.Tests;

/// <summary>
/// Semantic-Model-Tests für das <c>cancel</c>-Kantenziel (S2) — der versionsunabhängige Modell-Kern.
/// Anders als alle anderen Ziele hat <c>cancel</c> keine Deklaration (E4): die Zielreferenz ist eine
/// stets unaufgelöste <see cref="ICancelNodeReferenceSymbol"/>, erscheint nie als <see cref="Call"/> und
/// ist über <see cref="EdgeExtensions.TargetsCancel"/> erkennbar. Die Versions-Wirksamkeit (Nav5xxx),
/// die Stellen-Restriktion und das Codegen-Gating sind spätere Schritte (S3/S4) — das Modell baut den
/// Cancel-Ausgang unabhängig von <c>#version</c>.
/// </summary>
[TestFixture]
public class CancelSemanticTests {

    const string TriggerNav = """

                              task Sample
                              {
                                  init Init1;
                                  exit Exit;
                                  view View;

                                  Init1 --> View;
                                  View  --> Exit on OnOk;
                                  View  --> cancel on OnEscape;
                              }

                              """;

    const string ChoiceNav = """

                             task Sample
                             {
                                 init Init1;
                                 exit Exit;
                                 view View;
                                 choice C;

                                 Init1 --> C;
                                 C     --> View;
                                 C     --> cancel if "Abbruch";
                                 View  --> Exit on OnOk;
                             }

                             """;

    [Test]
    public void CancelTarget_OnDirectTriggerEdge_ResolvesToCancelReference() {

        var task = ParseModel(TriggerNav).TryFindTaskDefinition("Sample");
        var view = task.TryFindNode<IViewNodeSymbol>("View");

        var cancelEdge = view.Outgoings.Single(e => e.TargetReference?.Name == "cancel");

        Assert.That(cancelEdge.TargetReference,                   Is.InstanceOf<ICancelNodeReferenceSymbol>());
        Assert.That(cancelEdge.TargetReference!.Declaration,      Is.Null, "cancel hat keine Deklaration (E4).");
        Assert.That(cancelEdge.TargetReference.NodeReferenceType, Is.EqualTo(NodeReferenceType.Target));
        Assert.That(cancelEdge.TargetsCancel(),                   Is.True);
        Assert.That(cancelEdge.TargetReference.Edge,              Is.SameAs(cancelEdge), "Die Referenz kennt ihre Kante.");
    }

    [Test]
    public void CancelTarget_OnChoiceArm_ResolvesToCancelReference() {

        var task   = ParseModel(ChoiceNav).TryFindTaskDefinition("Sample");
        var choice = task.TryFindNode<IChoiceNodeSymbol>("C");

        var cancelArm = choice.Outgoings.Single(e => e.TargetsCancel());

        Assert.That(cancelArm.TargetReference,              Is.InstanceOf<ICancelNodeReferenceSymbol>());
        Assert.That(cancelArm.TargetReference!.Declaration, Is.Null);
    }

    [Test]
    public void CancelTarget_DoesNotReportNav0011() {

        // Die fehlende Deklaration ist bei cancel gewollt (E4) — sie darf nicht als "unauflösbarer Name"
        // gemeldet werden.
        var triggerIds = ParseModel(TriggerNav).Diagnostics.Select(d => d.Descriptor.Id).ToList();
        var choiceIds  = ParseModel(ChoiceNav).Diagnostics.Select(d => d.Descriptor.Id).ToList();

        Assert.That(triggerIds, Has.No.Member(DiagnosticDescriptors.Semantic.Nav0011CannotResolveNode0.Id),
                    "cancel an einer direkten Trigger-Kante darf kein Nav0011 auslösen.");
        Assert.That(choiceIds, Has.No.Member(DiagnosticDescriptors.Semantic.Nav0011CannotResolveNode0.Id),
                    "cancel an einem Choice-Arm darf kein Nav0011 auslösen.");
    }

    [Test]
    public void CancelTarget_ProducesNoCall() {

        // cancel hat keinen Knoten → kein Call (GetDirectCalls überspringt Ziele ohne aufgelöste
        // Deklaration). Der Cancel-Ausgang wird ausschließlich über TargetsCancel erkannt.
        var task = ParseModel(TriggerNav).TryFindTaskDefinition("Sample");
        var view = task.TryFindNode<IViewNodeSymbol>("View");

        var directCalls = view.Outgoings.GetDirectCalls().ToList();

        Assert.That(directCalls.Select(c => c.Node.Name), Has.No.Member("cancel"),
                    "cancel erscheint nie als Call.");
        Assert.That(directCalls.Select(c => c.Node.Name), Does.Contain("Exit"),
                    "Die Schwesterkante zum Exit bleibt ein regulärer Call.");
    }

    [Test]
    public void RegularUnresolvedTarget_StillReportsNav0011() {

        // Kontrolle: ein echter unauflösbarer Name feuert weiterhin Nav0011 — die cancel-Ausnahme
        // greift nicht versehentlich für jede fehlende Deklaration.
        var nav = """

                  task Sample
                  {
                      init Init1;

                      Init1 --> Nirgendwo;
                  }

                  """;

        var ids = ParseModel(nav).Diagnostics.Select(d => d.Descriptor.Id).ToList();

        Assert.That(ids, Has.Member(DiagnosticDescriptors.Semantic.Nav0011CannotResolveNode0.Id));
    }

    static CodeGenerationUnit ParseModel(string source) {
        var syntax = Syntax.ParseCodeGenerationUnit(source);
        return CodeGenerationUnit.FromCodeGenerationUnitSyntax(syntax);
    }

}
