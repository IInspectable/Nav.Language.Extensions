#region Using Directives

using System.Threading;

using NUnit.Framework;

using Pharmatechnik.Nav.Language.CodeAnalysis.FindSymbols;

#endregion

namespace Nav.Language.CodeAnalysis.Tests.Exit;

/// <summary>
/// Nav → C#: der Sprung von einem Exit-Punkt (<c>{TaskNode}:{Exit}</c>) auf die generierte
/// <c>After{TaskNode}Logic</c>. Die After-Logik existiert pro Task-Knoten (nicht pro Exit-Punkt), daher
/// landen beide Exit-Punkte E1/E2 auf DERSELBEN <c>AfterSubLogic</c> — der Sprung ist in dieser Richtung
/// eindeutig.
/// </summary>
[TestFixture]
public class ExitGoToCSharpTests {

    [Test]
    public void GeneratedStage_ResolvesWfsBaseWithAfterLogic() {

        // Absicherung der Bühne: Die generierte {Task}WFSBase muss die After{TaskNode}Logic tragen — daran
        // hängt der Nav→C#-Sprung. Ein vollständig fehlerfreier Compile ist hier bewusst NICHT die Messlatte.
        var ctx = CodeAnalysisTestContext.FromNav(ExitFixtures.ExitFlow);

        var exitInfo = ctx.ExitInfo("Sub");
        var wfsBase  = ctx.ResolveGeneratedType(exitInfo.ContainingTask.FullyQualifiedWfsBaseName);

        Assert.That(wfsBase,                                          Is.Not.Null);
        Assert.That(wfsBase.GetMembers(exitInfo.AfterLogicMethodName), Is.Not.Empty);
    }

    [Test]
    public void ExitPoint_JumpsToAfterLogic() {

        var ctx = CodeAnalysisTestContext.FromNav(ExitFixtures.ExitFlow);

        var location = LocationFinder.FindTaskExitDeclarationLocationAsync(
                                          ctx.Project, ctx.ExitInfo("Sub"), CancellationToken.None)
                                     .GetAwaiter().GetResult();

        GoldenAssert.Match(location, ctx, nameof(ExitPoint_JumpsToAfterLogic),
                           NavigationDirection.NavToCSharp,
                           """
                           F12 auf einen Exit-Punkt von Sub (Sub:E1 / Sub:E2) landet auf der gemeinsamen AfterSubLogic
                           im konkreten WFS — der Golden pinnt Datei + exakten Span.
                           """);
    }

    [Test]
    public void MissingWfs_ThrowsLocationNotFound() {

        // Negativpfad: Der Exit-Anker stammt aus ExitFlow, die Roslyn-Bühne aber aus einem fremden Task
        // OHNE dessen {Task}WFSBase. Der LocationFinder muss die fehlende Zielklasse als
        // LocationNotFoundException melden, statt still null bzw. eine falsche Stelle zu liefern.
        var exitInfo       = CodeAnalysisTestContext.FromNav(ExitFixtures.ExitFlow).ExitInfo("Sub");
        var foreignProject = CodeAnalysisTestContext.ForeignProject();

        Assert.That(
            () => LocationFinder.FindTaskExitDeclarationLocationAsync(foreignProject, exitInfo, CancellationToken.None)
                                .GetAwaiter().GetResult(),
            Throws.TypeOf<LocationNotFoundException>());
    }
}
