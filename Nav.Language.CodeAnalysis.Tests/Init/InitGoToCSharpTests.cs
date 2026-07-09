#region Using Directives

using System.Linq;
using System.Threading;

using NUnit.Framework;

using Pharmatechnik.Nav.Language.CodeAnalysis.Annotation;
using Pharmatechnik.Nav.Language.CodeAnalysis.FindSymbols;

#endregion

namespace Nav.Language.CodeAnalysis.Tests.Init;

/// <summary>
/// Nav → C# und C# → C# rund um den <c>init</c>-Knoten:
/// <list type="bullet">
///   <item>Nav → C#: vom eigenen <c>init X</c>-Knoten auf die generierte <c>{Begin}Logic</c>
///         (<see cref="LocationFinder.FindTaskBeginDeclarationLocationAsync"/>);</item>
///   <item>C# → C#: von der Aufrufstelle <c>next.Begin{Child}()</c> eines geöffneten Sub-Tasks auf dessen
///         <c>{Child}</c>-<c>BeginLogic</c>
///         (<see cref="LocationFinder.FindCallBeginLogicDeclarationLocationsAsync"/>);</item>
///   <item>C# → C#: von derselben Aufrufstelle auf die zugehörige <c>After{Child}</c>-Rücksprungmethode
///         (<see cref="LocationFinder.FindInitCallAfterLocation"/>).</item>
/// </list>
/// </summary>
[TestFixture]
public class InitGoToCSharpTests {

    [Test]
    public void GeneratedStage_ResolvesWfsBaseWithBeginLogic() {

        // Absicherung der Bühne: Die generierte {Task}WFSBase muss die {Begin}Logic tragen — daran hängt
        // der Nav→C#-Sprung. Ein vollständig fehlerfreier Compile ist hier bewusst NICHT die Messlatte.
        var ctx = CodeAnalysisTestContext.FromNav(InitFixtures.InitFlow);

        var initInfo = ctx.InitInfo("Init1");
        var wfsBase  = ctx.ResolveGeneratedType(initInfo.ContainingTask.FullyQualifiedWfsBaseName);

        Assert.That(wfsBase,                                        Is.Not.Null);
        Assert.That(wfsBase.GetMembers(initInfo.BeginLogicMethodName), Is.Not.Empty);
    }

    [Test]
    public void InitNode_JumpsToBeginLogic() {

        var ctx = CodeAnalysisTestContext.FromNav(InitFixtures.InitFlow);

        var location = LocationFinder.FindTaskBeginDeclarationLocationAsync(
                                          ctx.Project, ctx.InitInfo("Init1"), CancellationToken.None)
                                     .GetAwaiter().GetResult();

        GoldenAssert.Match(location, ctx, nameof(InitNode_JumpsToBeginLogic),
                           NavigationDirection.NavToCSharp,
                           """
                           F12 auf `init Init1` landet auf der zugehörigen BeginLogic (via <NavInit>Init1) im konkreten
                           WFS — der Golden pinnt Datei + exakten Span, nicht bloß irgendein BeginLogic-Overload.
                           """);
    }

    [Test]
    public void InitCallSite_JumpsToChildBeginLogic() {

        var ctx = CodeAnalysisTestContext.FromNav(InitFixtures.InitFlow, InitFixtures.InitFlowUserCode);

        // Annotationsgetriebener Pfad: von der Aufrufstelle next.BeginChild() über die IBeginChildWFS-
        // Schnittstelle auf die BeginLogic der lokal definierten ChildWFS.
        // Versions-Annahme: dieser Pfad kennt keine Sprach-Version und sucht die BeginLogic der
        // Default-Generation; grün gegen die #version-2-Fixture nur, weil V2 == V1 in diesen Namen — als
        // ausführbare Invariante gepinnt in CallSiteVersionAssumptionTests.
        var location = LocationFinder.FindCallBeginLogicDeclarationLocationsAsync(
                                          ctx.Project, ctx.InitCallAnnotation("IBeginChildWFS"), CancellationToken.None)
                                     .GetAwaiter().GetResult();

        GoldenAssert.Match(location, ctx, nameof(InitCallSite_JumpsToChildBeginLogic),
                           NavigationDirection.CSharpToCSharp,
                           "Von der Aufrufstelle next.BeginChild() auf die BeginLogic der geöffneten ChildWFS (lokal definierter Sub-Task).");
    }

    [Test]
    public void InitCallSite_JumpsToAfterMethod() {

        var ctx = CodeAnalysisTestContext.FromNav(InitFixtures.InitFlow, InitFixtures.InitFlowUserCode);

        // Zweites Ziel der Aufrufstelle next.BeginChild(): die zugehörige After{Node}-Rücksprungmethode.
        // Der Begin-Prefix wird abgestreift (BeginChild → Child) und die <NavExit>-Annotation mit
        // passendem ExitTaskName + Task/Datei-Verankerung gesucht — VS-freie Suchlogik im LocationFinder,
        // dieselbe, die der NavInitCallLocationInfoProvider (VS) für sein zweites Ziel nutzt.
        var location = LocationFinder.FindInitCallAfterLocation(
            ctx.InitCallAnnotation("IBeginChildWFS"),
            ctx.ReadAnnotations().OfType<NavExitAnnotation>());

        Assert.That(location, Is.Not.Null);
        GoldenAssert.Match(location, ctx, nameof(InitCallSite_JumpsToAfterMethod),
                           NavigationDirection.CSharpToCSharp,
                           """
                           Von der Aufrufstelle next.BeginChild() auf die zugehörige After{Node}-Rücksprungmethode —
                           der Golden pinnt Datei + exakten Span (Anzeigename = Methoden-Bezeichner).
                           """);
    }

    [Test]
    public void InitCallSite_NoMatchingExit_ReturnsNull() {

        var ctx = CodeAnalysisTestContext.FromNav(InitFixtures.InitFlow, InitFixtures.InitFlowUserCode);

        // Ohne passende Exit-Annotation gibt es kein zweites Ziel: FindInitCallAfterLocation liefert null
        // (der Host bietet dann nur das BeginLogic-Ziel an) — bewusst KEINE LocationNotFoundException.
        var location = LocationFinder.FindInitCallAfterLocation(
            ctx.InitCallAnnotation("IBeginChildWFS"),
            Enumerable.Empty<NavExitAnnotation>());

        Assert.That(location, Is.Null);
    }

    [Test]
    public void MissingWfs_ThrowsLocationNotFound() {

        // Negativpfad: Der Init-Anker stammt aus InitFlow, die Roslyn-Bühne aber aus einem fremden Task
        // OHNE dessen {Task}WFSBase. Der LocationFinder muss die fehlende Zielklasse als
        // LocationNotFoundException melden, statt still null bzw. eine falsche Stelle zu liefern.
        var initInfo       = CodeAnalysisTestContext.FromNav(InitFixtures.InitFlow).InitInfo("Init1");
        var foreignProject = CodeAnalysisTestContext.ForeignProject();

        Assert.That(
            () => LocationFinder.FindTaskBeginDeclarationLocationAsync(foreignProject, initInfo, CancellationToken.None)
                                .GetAwaiter().GetResult(),
            Throws.TypeOf<LocationNotFoundException>());
    }

    [Test]
    public void MissingCallBeginLogic_ThrowsLocationNotFound() {

        // Negativpfad des annotationsgetriebenen C#→C#-Sprungs: die <NavInitCall>-Aufrufstelle stammt aus
        // InitFlow, die Roslyn-Bühne aber aus einem fremden Task OHNE das Ziel-Interface IBeginChildWFS. Der
        // LocationFinder muss das fehlende Begin-Interface als LocationNotFoundException melden.
        var initCall       = CodeAnalysisTestContext.FromNav(InitFixtures.InitFlow, InitFixtures.InitFlowUserCode)
                                                    .InitCallAnnotation("IBeginChildWFS");
        var foreignProject = CodeAnalysisTestContext.ForeignProject();

        Assert.That(
            () => LocationFinder.FindCallBeginLogicDeclarationLocationsAsync(foreignProject, initCall, CancellationToken.None)
                                .GetAwaiter().GetResult(),
            Throws.TypeOf<LocationNotFoundException>());
    }
}
