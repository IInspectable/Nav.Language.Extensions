#region Using Directives

using System.Threading;

using NUnit.Framework;

using Pharmatechnik.Nav.Language.CodeGen;
using Pharmatechnik.Nav.Language.CodeAnalysis.Annotation;
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
    public void AfterLogic_JumpsToAllBeginCallSites() {

        var ctx = CodeAnalysisTestContext.FromNav(ExitFixtures.ExitFlow, ExitFixtures.ExitFlowUserCode);

        // Gegenrichtung C#→C# (Analogon zum Choice-Aufrufer): von der After{TaskNode}Logic klassenweit auf
        // ALLE C#-Aufrufstellen des Begin{Node}-Wrappers (next.BeginSub()). Geprüft wird die ECHTE VS-freie
        // Suchlogik LocationFinder.FindCallerLocations — dieselbe, die der NavExitBeginCallerLocationInfoProvider
        // (VS) nutzt: über alle (partiellen) Deklarationen der konkreten {Task}WFS (nur sie umspannt den
        // Nutzer-partial), gefiltert per TaskName/NavFileName + abgestreiftem Begin-Prefix == ExitTaskName.
        var exitAnnotation = ctx.ExitAnnotation("Sub");
        var wfsSymbol      = ctx.ResolveGeneratedType(ctx.TaskInfo("ExitFlow").FullyQualifiedWfsName);
        var beginPrefix    = CodeGenFacts.BeginMethodPrefix;

        var callers = LocationFinder.FindCallerLocations(
                                        ctx.Project, wfsSymbol,
                                        call => call is NavInitCallAnnotation                        &&
                                                call.TaskName    == exitAnnotation.TaskName          &&
                                                call.NavFileName == exitAnnotation.NavFileName        &&
                                                call.Identifier.Identifier.Text == beginPrefix + exitAnnotation.ExitTaskName,
                                        CancellationToken.None)
                                   .GetAwaiter().GetResult();

        GoldenAssert.Match(callers, ctx, nameof(AfterLogic_JumpsToAllBeginCallSites),
                           NavigationDirection.CSharpToCSharp,
                           """
                           Rücksprung von AfterSubLogic auf die C#-Aufrufstelle next.BeginSub() des Begin-Wrappers
                           (klassenweit, inkl. partial) — der Golden pinnt den Aufrufer-Span.
                           """);
    }

    [Test]
    public void AfterLogic_NoCallers_ReturnsEmpty() {

        // Nicht-Wirf-Contract von FindCallerLocations (bewusst anders als die werfenden Finder): ohne
        // ExitFlowUserCode existiert KEINE next.BeginSub()-Aufrufstelle. Derselbe realistische Filter wie in
        // AfterLogic_JumpsToAllBeginCallSites muss dann eine LEERE Liste liefern — nicht null und keine
        // LocationNotFoundException. Der Host bietet in diesem Fall schlicht keine Aufrufer-Navigation an.
        var ctx = CodeAnalysisTestContext.FromNav(ExitFixtures.ExitFlow);

        var exitAnnotation = ctx.ExitAnnotation("Sub");
        var wfsSymbol      = ctx.ResolveGeneratedType(ctx.TaskInfo("ExitFlow").FullyQualifiedWfsName);
        var beginPrefix    = CodeGenFacts.BeginMethodPrefix;

        var callers = LocationFinder.FindCallerLocations(
                                        ctx.Project, wfsSymbol,
                                        call => call is NavInitCallAnnotation                        &&
                                                call.TaskName    == exitAnnotation.TaskName          &&
                                                call.NavFileName == exitAnnotation.NavFileName        &&
                                                call.Identifier.Identifier.Text == beginPrefix + exitAnnotation.ExitTaskName,
                                        CancellationToken.None)
                                   .GetAwaiter().GetResult();

        Assert.That(callers, Is.Not.Null.And.Empty);
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
