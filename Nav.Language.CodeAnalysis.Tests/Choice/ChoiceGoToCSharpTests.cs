#region Using Directives

using System.Linq;
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
public class ChoiceGoToCSharpTests {

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

        GoldenAssert.Match(NavigationSnapshot.Serialize(location, ctx), nameof(ChoiceNode_JumpsToChoiceLogicOverride),
                           NavigationDirection.NavToCSharp,
                           """
                           F12 auf `choice Choice_Retry` landet auf dem Override Choice_RetryLogic im konkreten WFS —
                           der Golden pinnt Datei + exakten Span (nicht nur „irgendwo steht Choice_RetryLogic").
                           """);
    }

    [Test]
    public void SecondChoiceNode_JumpsToItsOwnChoiceLogic() {

        var ctx = CodeAnalysisTestContext.FromNav(ChoiceFixtures.ChoiceFlow);

        var location = LocationFinder.FindChoiceLogicDeclarationLocationAsync(
                                          ctx.Project, ctx.ChoiceInfo("Choice_Escalate"), CancellationToken.None)
                                     .GetAwaiter().GetResult();

        GoldenAssert.Match(NavigationSnapshot.Serialize(location, ctx), nameof(SecondChoiceNode_JumpsToItsOwnChoiceLogic),
                           NavigationDirection.NavToCSharp,
                           "F12 auf den zweiten Knoten `choice Choice_Escalate` springt auf dessen eigene Choice_EscalateLogic.");
    }

    [Test]
    public void ChoiceCallSite_JumpsToChoiceLogic() {

        var ctx = CodeAnalysisTestContext.FromNav(ChoiceFixtures.ChoiceFlow, ChoiceFixtures.ChoiceFlowUserCode);

        var location = LocationFinder.FindCallChoiceLogicDeclarationLocationAsync(
                                          ctx.Project, ctx.ChoiceCallAnnotation("Choice_Retry"), CancellationToken.None)
                                     .GetAwaiter().GetResult();

        GoldenAssert.Match(NavigationSnapshot.Serialize(location, ctx), nameof(ChoiceCallSite_JumpsToChoiceLogic),
                           NavigationDirection.CSharpToCSharp,
                           "Annotationsgetriebener Pfad: von der Aufrufstelle next.Choice_Retry(…) auf die Choice_RetryLogic.");
    }

    [Test]
    public void ChoiceLogic_JumpsToAllForwardCallSites() {

        var ctx = CodeAnalysisTestContext.FromNav(ChoiceFixtures.ChoiceFlow, ChoiceFixtures.ChoiceFlowUserCode);

        // Gegenrichtung zum Aufrufstellen-Sprung (analog After→BeginXY-Aufrufer): von der {Choice}Logic
        // klassenweit auf ALLE next.Choice_Retry(…)-Aufrufstellen. Dieselbe VS-freie Annotationssuche, die
        // der NavChoiceCallerLocationInfoProvider (VS) im Kern nutzt.
        var locations = ctx.ChoiceCallAnnotations("Choice_Retry")
                           .Select(call => LocationFinder.ToLocation(call.Identifier.GetLocation()))
                           .Where(location => location != null)
                           .ToList();

        Assert.That(locations, Has.Count.EqualTo(2),
                    "Erwartet: beide next.Choice_Retry(…)-Aufrufstellen im Nutzer-Code.");

        GoldenAssert.Match(NavigationSnapshot.Serialize(locations, ctx), nameof(ChoiceLogic_JumpsToAllForwardCallSites),
                           NavigationDirection.CSharpToCSharp,
                           """
                           Rücksprung von Choice_RetryLogic auf ALLE next.Choice_Retry(…)-Aufrufstellen
                           (klassenweit, inkl. partial) — der Golden pinnt beide Aufrufer-Spans.
                           """);
    }
}
