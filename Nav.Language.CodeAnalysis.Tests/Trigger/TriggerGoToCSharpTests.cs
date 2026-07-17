#region Using Directives

using System.Threading;

using NUnit.Framework;

using Pharmatechnik.Nav.Language.CodeAnalysis.FindSymbols;

#endregion

namespace Nav.Language.CodeAnalysis.Tests.Trigger;

/// <summary>
/// Nav → C#: der Sprung vom Signal-Trigger (<c>… on OnX</c>) auf die generierte <c>{Trigger}Logic</c>-
/// Methode. Der <c>LocationFinder</c> steigt von der generierten <c>{Task}WFSBase</c> zu den abgeleiteten
/// Klassen ab und findet dort die überschreibende <c>{Trigger}Logic</c>.
/// </summary>
[TestFixture]
public class TriggerGoToCSharpTests {

    [Test]
    public void GeneratedStage_ResolvesWfsBaseWithTriggerLogic() {

        // Absicherung der Bühne: Die generierte {Task}WFSBase muss die {Trigger}Logic tragen — daran hängt
        // der Nav→C#-Sprung. Ein vollständig fehlerfreier Compile ist hier bewusst NICHT die Messlatte.
        var ctx = CodeAnalysisTestContext.FromNav(TriggerFixtures.TriggerFlow);

        var wfsBase = ctx.ResolveGeneratedType(ctx.TriggerInfo("OnOpen").ContainingTask.FullyQualifiedWfsBaseName);

        Assert.That(wfsBase,                          Is.Not.Null);
        Assert.That(wfsBase.GetMembers("OnOpenLogic"), Is.Not.Empty);
    }

    [Test]
    public void TriggerTransition_JumpsToTriggerLogic() {

        var ctx = CodeAnalysisTestContext.FromNav(TriggerFixtures.TriggerFlow);

        var location = LocationFinder.FindTriggerDeclarationLocationsAsync(
                                          ctx.Project, ctx.TriggerInfo("OnOpen"), CancellationToken.None)
                                     .GetAwaiter().GetResult();

        GoldenAssert.Match(location, ctx, nameof(TriggerTransition_JumpsToTriggerLogic),
                           NavigationDirection.NavToCSharp,
                           """
                           F12 auf den Trigger `on OnOpen` landet auf der OnOpenLogic im konkreten WFS —
                           der Golden pinnt Datei + exakten Span (nicht nur „irgendwo steht OnOpenLogic").
                           """);
    }

    [Test]
    public void SecondTrigger_JumpsToItsOwnTriggerLogic() {

        var ctx = CodeAnalysisTestContext.FromNav(TriggerFixtures.TriggerFlow);

        var location = LocationFinder.FindTriggerDeclarationLocationsAsync(
                                          ctx.Project, ctx.TriggerInfo("OnClose"), CancellationToken.None)
                                     .GetAwaiter().GetResult();

        GoldenAssert.Match(location, ctx, nameof(SecondTrigger_JumpsToItsOwnTriggerLogic),
                           NavigationDirection.NavToCSharp,
                           "F12 auf den zweiten Trigger `on OnClose` springt zielgenau auf dessen eigene OnCloseLogic.");
    }

    [Test]
    public void MissingWfs_ThrowsLocationNotFound() {

        // Negativpfad: Der Trigger-Anker stammt aus TriggerFlow, die Roslyn-Bühne aber aus einem fremden
        // Task OHNE dessen {Task}WFSBase. Der LocationFinder muss die fehlende Zielklasse als
        // LocationNotFoundException melden, statt still null bzw. eine falsche Stelle zu liefern.
        var triggerInfo    = CodeAnalysisTestContext.FromNav(TriggerFixtures.TriggerFlow).TriggerInfo("OnOpen");
        var foreignProject = CodeAnalysisTestContext.ForeignProject();

        Assert.That(
            () => LocationFinder.FindTriggerDeclarationLocationsAsync(foreignProject, triggerInfo, CancellationToken.None)
                                .GetAwaiter().GetResult(),
            Throws.TypeOf<LocationNotFoundException>());
    }
}
