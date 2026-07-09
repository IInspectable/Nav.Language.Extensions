#region Using Directives

using System.Threading;

using NUnit.Framework;

using Pharmatechnik.Nav.Language.CodeAnalysis.FindSymbols;

#endregion

namespace Nav.Language.CodeAnalysis.Tests.Choice;

/// <summary>
/// Nav → C#: der Sprung vom <c>choice X</c>-Knoten (bzw. einer C#-Aufrufstelle) auf die generierte
/// <c>{Choice}Logic</c>-Implementierung.
/// </summary>
[TestFixture]
public class ChoiceGoToTests {

    [Test]
    public void GeneratedStage_ResolvesWfsBaseWithChoiceLogic() {

        // Absicherung der Bühne: Die generierte {Task}WFSBase muss in der Kompilation auflösbar sein und
        // die abstrakte {Choice}Logic tragen — genau daran hängen die Nav→C#-Sprünge (Abstieg von der
        // Basis auf die abgeleiteten Klassen). Ein vollständig fehlerfreier Compile ist hier bewusst NICHT
        // die Messlatte: die per taskref referenzierten A/Msg-Typen entstünden erst im Mehrdatei-Build.
        var ctx = CodeAnalysisTestContext.FromNav(ChoiceFixtures.ChoiceFlow);

        var wfsBase = ctx.ResolveGeneratedType(ctx.WfsBaseFullyQualifiedName("Choice_Retry"));

        Assert.That(wfsBase,                             Is.Not.Null);
        Assert.That(wfsBase.GetMembers("Choice_RetryLogic"), Is.Not.Empty);
    }

    [Test]
    public void ChoiceNode_JumpsToChoiceLogicOverride() {

        var ctx = CodeAnalysisTestContext.FromNav(ChoiceFixtures.ChoiceFlow);

        var location = LocationFinder.FindChoiceLogicDeclarationLocationAsync(
                                          ctx.Project, ctx.ChoiceInfo("Choice_Retry"), CancellationToken.None)
                                     .GetAwaiter().GetResult();

        // F12 auf `choice Choice_Retry` landet auf dem Override Choice_RetryLogic im konkreten WFS.
        Assert.That(ctx.TextAt(location),        Is.EqualTo("Choice_RetryLogic"));
        Assert.That(ctx.IsInConcreteWfs(location), Is.True);
    }

    [Test]
    public void SecondChoiceNode_JumpsToItsOwnChoiceLogic() {

        var ctx = CodeAnalysisTestContext.FromNav(ChoiceFixtures.ChoiceFlow);

        var location = LocationFinder.FindChoiceLogicDeclarationLocationAsync(
                                          ctx.Project, ctx.ChoiceInfo("Choice_Escalate"), CancellationToken.None)
                                     .GetAwaiter().GetResult();

        Assert.That(ctx.TextAt(location),          Is.EqualTo("Choice_EscalateLogic"));
        Assert.That(ctx.IsInConcreteWfs(location), Is.True);
    }

    [Test]
    public void ChoiceCallSite_JumpsToChoiceLogic() {

        var ctx = CodeAnalysisTestContext.FromNav(ChoiceFixtures.ChoiceFlow, ChoiceFixtures.ChoiceFlowUserCode);

        // Annotationsgetriebener C#→C#-Pfad: von der Aufrufstelle next.Choice_Retry(…) auf die Logic.
        var location = LocationFinder.FindCallChoiceLogicDeclarationLocationAsync(
                                          ctx.Project, ctx.ChoiceCallAnnotation("Choice_Retry"), CancellationToken.None)
                                     .GetAwaiter().GetResult();

        Assert.That(ctx.TextAt(location),          Is.EqualTo("Choice_RetryLogic"));
        Assert.That(ctx.IsInConcreteWfs(location), Is.True);
    }
}
