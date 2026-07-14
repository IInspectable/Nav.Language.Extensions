using System.Linq;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;

// ReSharper disable PossibleNullReferenceException

namespace Nav.Language.Tests;

/// <summary>
/// Semantic-Model-Tests für die Continuation (<c>… o-^ Task</c> / <c>… --^ Task</c>) und die
/// Choice-Parameter (<c>choice X [params …]</c>) — der versionsunabhängige Semantic-Model-Kern (S2).
/// Die Versions-Wirksamkeit (Nav5000) und die Struktur-Analyzer (Nav0120/0121/0122) sind hier bewusst
/// nicht im Spiel: das Modell baut die Continuation unabhängig von <c>#version</c>.
/// </summary>
[TestFixture]
public class ContinuationSemanticTests {

    const string SampleNav = """

        task Sample [namespaceprefix N]
        {
            init Init1;
            exit Exit;
            task Msg;
            view View;
            choice Choice_Retry [params string reason];

            Init1        --> Choice_Retry;
            Choice_Retry --> View;
            Choice_Retry --> View o-^ Msg if "Fehler";
            Msg:Exit     --> View --^ Msg;
            View         --> Exit on OnClose;
        }
                
        """;

    [Test]
    public void ContinuationTransitionIsBuiltOnCarryingEdge() {

        var task = ParseModel(SampleNav).TryFindTaskDefinition("Sample");

        var plainEdge = task.ChoiceTransitions.Single(t => t.SourceReference.Name == "Choice_Retry" &&
                                                           t.TargetReference.Name == "View"          &&
                                                           t.ContinuationTransition == null);
        Assert.That(plainEdge, Is.Not.Null, "Die plain-Kante trägt keine Continuation.");

        var continuationEdge = task.ChoiceTransitions.Single(t => t.SourceReference.Name == "Choice_Retry" &&
                                                                 t.ContinuationTransition != null);

        var continuation = continuationEdge.ContinuationTransition;
        Assert.That(continuation.EdgeMode.EdgeMode,     Is.EqualTo(EdgeMode.Modal), "o-^ ist eine Modal-Continuation.");
        Assert.That(continuation.TargetReference.Name,  Is.EqualTo("Msg"));
        Assert.That(continuation.SourceReference.Name,  Is.EqualTo("View"), "Quelle der Continuation ist der tragende GUI-Knoten.");
    }

    [Test]
    public void GotoContinuationOnExitTransitionUsesGotoMode() {

        var task = ParseModel(SampleNav).TryFindTaskDefinition("Sample");

        var exitEdge = task.ExitTransitions.Single(t => t.ContinuationTransition != null);

        Assert.That(exitEdge.ContinuationTransition.EdgeMode.EdgeMode,    Is.EqualTo(EdgeMode.Goto), "--^ ist eine Goto-Continuation.");
        Assert.That(exitEdge.ContinuationTransition.TargetReference.Name, Is.EqualTo("Msg"));
    }

    [Test]
    public void IsContinuationDistinguishesContinuationFromRegularEdge() {

        var task = ParseModel(SampleNav).TryFindTaskDefinition("Sample");

        // Reguläre Kante (Choice_Retry --> View, ohne Anhang): kein Continuation-Modus.
        var plainEdge = task.ChoiceTransitions.Single(t => t.TargetReference.Name    == "View" &&
                                                           t.ContinuationTransition == null);
        Assert.That(plainEdge.EdgeMode.IsContinuation, Is.False, "--> ist eine reguläre Kante.");

        // o-^ und --^ tragen denselben EdgeMode (Modal/Goto) wie reguläre Kanten — erst IsContinuation
        // unterscheidet sie. Genau daran hängt die Icon-Auswahl (Modal/GoTo Continuation statt Edge).
        var modalContinuation = task.ChoiceTransitions.Single(t => t.ContinuationTransition != null)
                                    .ContinuationTransition.EdgeMode;
        Assert.That(modalContinuation.EdgeMode,       Is.EqualTo(EdgeMode.Modal));
        Assert.That(modalContinuation.IsContinuation, Is.True, "o-^ ist eine Continuation.");

        var gotoContinuation = task.ExitTransitions.Single(t => t.ContinuationTransition != null)
                                   .ContinuationTransition.EdgeMode;
        Assert.That(gotoContinuation.EdgeMode,       Is.EqualTo(EdgeMode.Goto));
        Assert.That(gotoContinuation.IsContinuation, Is.True, "--^ ist eine Continuation.");
    }

    [Test]
    public void ReachableContinuationCallsSurfaceTheFollowUpTask() {

        var task      = ParseModel(SampleNav).TryFindTaskDefinition("Sample");
        var initEdge  = task.TryFindNode<IInitNodeSymbol>("Init1").Outgoings.Single();

        // Init1 --> Choice_Retry löst die Choice auf; genau eine ihrer Ausgangskanten trägt eine
        // Continuation (o-^ Msg) → genau ein Continuation-Call auf Msg (Modal).
        var continuationCalls = initEdge.GetReachableContinuationCalls().ToList();

        Assert.That(continuationCalls.Select(c => c.Node.Name), Is.EquivalentTo(new[] { "Msg" }));
        Assert.That(continuationCalls.Single().EdgeMode.EdgeMode, Is.EqualTo(EdgeMode.Modal));
    }

    [Test]
    public void PlainAndContinuationEdgeToSameViewAreDistinctCalls() {

        var task      = ParseModel(SampleNav).TryFindTaskDefinition("Sample");
        var initEdge  = task.TryFindNode<IInitNodeSymbol>("Init1").Outgoings.Single();

        // Beide Choice-Kanten zeigen (goto) auf View; sie unterscheiden sich nur in der Continuation.
        // Der ContinuationCall im Gleichheits-/Hash-Kontrakt von Call hält sie auseinander (Nav0222-Basis).
        var viewCalls = initEdge.GetReachableCalls().Where(c => c.Node.Name == "View").ToList();

        Assert.That(viewCalls, Has.Count.EqualTo(2));
        Assert.That(viewCalls.Count(c => c.ContinuationCall == null), Is.EqualTo(1));
        Assert.That(viewCalls.Count(c => c.ContinuationCall != null), Is.EqualTo(1));
    }

    [Test]
    public void ChoiceParametersAreReachableViaSyntax() {

        var task   = ParseModel(SampleNav).TryFindTaskDefinition("Sample");
        var choice = task.TryFindNode<IChoiceNodeSymbol>("Choice_Retry");

        var parameters = choice.Syntax.CodeParamsDeclaration?.ParameterList;

        Assert.That(parameters,                        Is.Not.Null, "choice [params …] muss am Choice-Knoten ankommen.");
        Assert.That(parameters.Count,                  Is.EqualTo(1));
        Assert.That(parameters[0].Identifier.ToString(), Is.EqualTo("reason"));
        Assert.That(parameters[0].Type.ToString().Trim(), Is.EqualTo("string"));
    }

    [Test]
    public void ViewCarryingContinuationIsNotReportedAsDeadEnd() {

        // Der View wird erreicht (Init1 --> View) und trägt eine Continuation (o-^ Msg), hat aber
        // keine Trigger-Transition. Die Continuation ist zwar keine Outgoing-Kante des Views, führt
        // den Ablauf aber in den Folge-Task Msg weiter → keine Sackgasse (Nav0117/Nav1019).
        var nav = """

            task Sample
            {
                init Init1;
                task Msg;
                view View;

                Init1 --> View o-^ Msg;
            }

            """;

        var ids = ParseModel(nav).Diagnostics.Select(d => d.Descriptor.Id).ToList();

        Assert.That(ids, Has.No.Member(DiagnosticDescriptors.Semantic.Nav0117ViewNode0HasNoOutgoingEdges.Id),
                    "Ein View, der eine Continuation trägt, ist keine Sackgasse.");
        Assert.That(ids, Has.No.Member(DiagnosticDescriptors.DeadCode.Nav1019ViewNode0HasNoOutgoingEdges.Id),
                    "Das Dead-Code-Gegenstück darf ebenso wenig feuern.");
    }

    [Test]
    public void ViewWithoutContinuationOrTriggerIsReportedAsDeadEnd() {

        // Kontrolle: dieselbe Struktur OHNE Continuation → die Sackgassen-Diagnostik feuert wie gehabt.
        // Stellt sicher, dass die Unterdrückung nicht versehentlich immer greift.
        var nav = """

            task Sample
            {
                init Init1;
                view View;

                Init1 --> View;
            }

            """;

        var ids = ParseModel(nav).Diagnostics.Select(d => d.Descriptor.Id).ToList();

        Assert.That(ids, Has.Member(DiagnosticDescriptors.Semantic.Nav0117ViewNode0HasNoOutgoingEdges.Id),
                    "Ohne Continuation bleibt der View eine Sackgasse.");
    }

    [Test]
    public void DialogCarryingContinuationIsNotReportedAsDeadEnd() {

        // Continuation-Träger dürfen auch Dialoge sein (Show{Node} deckt View wie Dialog ab) → dieselbe
        // Unterdrückung greift für Nav0115/Nav1016.
        var nav = """

            task Sample
            {
                init Init1;
                task Msg;
                dialog Ask;

                Init1 --> Ask o-^ Msg;
            }

            """;

        var ids = ParseModel(nav).Diagnostics.Select(d => d.Descriptor.Id).ToList();

        Assert.That(ids, Has.No.Member(DiagnosticDescriptors.Semantic.Nav0115DialogNode0HasNoOutgoingEdges.Id),
                    "Ein Dialog, der eine Continuation trägt, ist keine Sackgasse.");
        Assert.That(ids, Has.No.Member(DiagnosticDescriptors.DeadCode.Nav1016DialogNode0HasNoOutgoingEdges.Id));
    }

    static CodeGenerationUnit ParseModel(string source) {
        var syntax = Syntax.ParseCodeGenerationUnit(source);
        return CodeGenerationUnit.FromCodeGenerationUnitSyntax(syntax);
    }

}
