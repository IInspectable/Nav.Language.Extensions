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

        GoldenAssert.Match(location, ctx, nameof(ChoiceNode_JumpsToChoiceLogicOverride),
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

        GoldenAssert.Match(location, ctx, nameof(SecondChoiceNode_JumpsToItsOwnChoiceLogic),
                           NavigationDirection.NavToCSharp,
                           "F12 auf den zweiten Knoten `choice Choice_Escalate` springt auf dessen eigene Choice_EscalateLogic.");
    }

    [Test]
    public void ChoiceCallSite_JumpsToChoiceLogic() {

        var ctx = CodeAnalysisTestContext.FromNav(ChoiceFixtures.ChoiceFlow, ChoiceFixtures.ChoiceFlowUserCode);

        var location = LocationFinder.FindCallChoiceLogicDeclarationLocationAsync(
                                          ctx.Project, ctx.ChoiceCallAnnotation("Choice_Retry"), CancellationToken.None)
                                     .GetAwaiter().GetResult();

        GoldenAssert.Match(location, ctx, nameof(ChoiceCallSite_JumpsToChoiceLogic),
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

        GoldenAssert.Match(locations, ctx, nameof(ChoiceLogic_JumpsToAllForwardCallSites),
                           NavigationDirection.CSharpToCSharp,
                           """
                           Rücksprung von Choice_RetryLogic auf ALLE next.Choice_Retry(…)-Aufrufstellen
                           (klassenweit, inkl. partial) — der Golden pinnt beide Aufrufer-Spans.
                           """);
    }

    [Test]
    public void EscalateCallSite_JumpsToChoiceLogic() {

        var ctx = CodeAnalysisTestContext.FromNav(ChoiceFixtures.ChoiceFlow, ChoiceFixtures.ChoiceFlowUserCode);

        // Choice→Choice als Aufrufstelle: next.Choice_Escalate(…) sitzt im Choice_RetryCallContext
        // (Choice_Retry --> Choice_Escalate). Sprung auf die Choice_EscalateLogic — der historisch fragile Fall.
        var location = LocationFinder.FindCallChoiceLogicDeclarationLocationAsync(
                                          ctx.Project, ctx.ChoiceCallAnnotation("Choice_Escalate"), CancellationToken.None)
                                     .GetAwaiter().GetResult();

        GoldenAssert.Match(location, ctx, nameof(EscalateCallSite_JumpsToChoiceLogic),
                           NavigationDirection.CSharpToCSharp,
                           "Choice→Choice: von der Aufrufstelle next.Choice_Escalate(…) (im Choice_Retry-Kontext) auf die Choice_EscalateLogic.");
    }

    [Test]
    public void EscalateChoiceLogic_JumpsToChoiceToChoiceCallSite() {

        var ctx = CodeAnalysisTestContext.FromNav(ChoiceFixtures.ChoiceFlow, ChoiceFixtures.ChoiceFlowUserCode);

        var locations = ctx.ChoiceCallAnnotations("Choice_Escalate")
                           .Select(call => LocationFinder.ToLocation(call.Identifier.GetLocation()))
                           .Where(location => location != null)
                           .ToList();

        Assert.That(locations, Has.Count.EqualTo(1),
                    "Erwartet: die eine next.Choice_Escalate(…)-Aufrufstelle (Choice→Choice) im Nutzer-Code.");

        GoldenAssert.Match(locations, ctx, nameof(EscalateChoiceLogic_JumpsToChoiceToChoiceCallSite),
                           NavigationDirection.CSharpToCSharp,
                           """
                           Rücksprung von Choice_EscalateLogic auf die Choice→Choice-Aufrufstelle
                           next.Choice_Escalate(…) (aus dem Choice_Retry-Kontext) — der historisch fragile Fall.
                           """);
    }

    [Test]
    public void MissingChoiceLogic_ThrowsLocationNotFound() {

        // Negativpfad: Der Choice-Anker stammt aus ChoiceFlow, die Roslyn-Bühne aber aus einem fremden
        // Task OHNE dessen {Task}WFSBase. Der LocationFinder darf dann NICHT still null bzw. eine falsche
        // Stelle liefern, sondern muss die fehlende Zielklasse als LocationNotFoundException melden.
        var choiceInfo     = CodeAnalysisTestContext.FromNav(ChoiceFixtures.ChoiceFlow).ChoiceInfo("Choice_Retry");
        var foreignProject = CodeAnalysisTestContext.ForeignProject();

        Assert.That(
            () => LocationFinder.FindChoiceLogicDeclarationLocationAsync(foreignProject, choiceInfo, CancellationToken.None)
                                .GetAwaiter().GetResult(),
            Throws.TypeOf<LocationNotFoundException>());
    }
}
