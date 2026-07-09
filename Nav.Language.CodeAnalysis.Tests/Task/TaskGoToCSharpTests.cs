#region Using Directives

using System.Threading;

using NUnit.Framework;

using Pharmatechnik.Nav.Language.CodeAnalysis.FindSymbols;

#endregion

namespace Nav.Language.CodeAnalysis.Tests.Task;

/// <summary>
/// Nav → C#: der Sprung von der <c>task X</c>-Deklaration auf die konkrete(n), abgeleitete(n)
/// <c>{Task}WFS</c>-Klasse(n). Der <c>LocationFinder</c> steigt von der generierten <c>{Task}WFSBase</c>
/// zu ihren abgeleiteten Klassen ab und blendet die <c>.generated.cs</c>-Basis dabei aus.
/// </summary>
[TestFixture]
public class TaskGoToCSharpTests {

    [Test]
    public void GeneratedStage_ResolvesWfsBase() {

        // Absicherung der Bühne: Die generierte {Task}WFSBase muss auflösbar sein — daran hängt der
        // Nav→C#-Abstieg auf die abgeleiteten {Task}WFS. Ein vollständig fehlerfreier Compile ist hier
        // bewusst NICHT die Messlatte.
        var ctx = CodeAnalysisTestContext.FromNav(TaskFixtures.TaskFlow);

        var wfsBase = ctx.ResolveGeneratedType(ctx.TaskInfo("TaskFlow").FullyQualifiedWfsBaseName);

        Assert.That(wfsBase, Is.Not.Null);
    }

    [Test]
    public void TaskDeclaration_JumpsToConcreteWfs() {

        var ctx = CodeAnalysisTestContext.FromNav(TaskFixtures.TaskFlow);

        var locations = LocationFinder.FindTaskDeclarationLocationsAsync(
                                          ctx.Project, ctx.TaskInfo("TaskFlow"), CancellationToken.None)
                                     .GetAwaiter().GetResult();

        GoldenAssert.Match(locations, ctx, nameof(TaskDeclaration_JumpsToConcreteWfs),
                           NavigationDirection.NavToCSharp,
                           """
                           F12 auf `task TaskFlow` landet auf der konkreten TaskFlowWFS (nicht der .generated.cs-Basis) —
                           der Golden pinnt Datei + exakten Span der Klassendeklaration.
                           """);
    }

    [Test]
    public void SecondTaskDeclaration_JumpsToItsOwnWfs() {

        var ctx = CodeAnalysisTestContext.FromNav(TaskFixtures.TaskFlow);

        var locations = LocationFinder.FindTaskDeclarationLocationsAsync(
                                          ctx.Project, ctx.TaskInfo("Helper"), CancellationToken.None)
                                     .GetAwaiter().GetResult();

        GoldenAssert.Match(locations, ctx, nameof(SecondTaskDeclaration_JumpsToItsOwnWfs),
                           NavigationDirection.NavToCSharp,
                           "F12 auf die zweite `task Helper`-Deklaration springt zielgenau auf HelperWFS (nicht TaskFlowWFS).");
    }

    [Test]
    public void MissingWfs_ThrowsLocationNotFound() {

        // Negativpfad: Der Task-Anker stammt aus TaskFlow, die Roslyn-Bühne aber aus einem fremden Task
        // OHNE dessen {Task}WFSBase. Der LocationFinder darf dann NICHT still null bzw. eine falsche Klasse
        // liefern, sondern muss die fehlende Zielklasse als LocationNotFoundException melden.
        var taskInfo       = CodeAnalysisTestContext.FromNav(TaskFixtures.TaskFlow).TaskInfo("TaskFlow");
        var foreignProject = CodeAnalysisTestContext.ForeignProject();

        Assert.That(
            () => LocationFinder.FindTaskDeclarationLocationsAsync(foreignProject, taskInfo, CancellationToken.None)
                                .GetAwaiter().GetResult(),
            Throws.TypeOf<LocationNotFoundException>());
    }
}
