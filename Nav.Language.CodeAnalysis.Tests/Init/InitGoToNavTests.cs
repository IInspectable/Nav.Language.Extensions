#region Using Directives

using System.Linq;
using System.Threading;

using NUnit.Framework;

using Pharmatechnik.Nav.Language.CodeAnalysis.Annotation;
using Pharmatechnik.Nav.Language.CodeAnalysis.FindSymbols;

#endregion

namespace Nav.Language.CodeAnalysis.Tests.Init;

/// <summary>
/// C# → Nav: der Rücksprung von der generierten <c>{Begin}Logic</c> zurück auf den <c>init X</c>-Knoten
/// im <c>.nav</c>. Die <c>&lt;NavInit&gt;</c>-Annotation wird dabei über den echten <c>AnnotationReader</c>
/// aus dem generierten Code gelesen.
/// </summary>
[TestFixture]
public class InitGoToNavTests {

    [Test]
    public void InitAnnotation_JumpsBackToInitNode() {

        var ctx = CodeAnalysisTestContext.FromNav(InitFixtures.InitFlow);

        var annotation = ctx.InitAnnotation("Init1");

        var location = LocationFinder.FindNavLocationsAsync(ctx.NavSource, annotation, CancellationToken.None)
                                     .GetAwaiter().GetResult()
                                     .Single();

        GoldenAssert.Match(location, ctx, nameof(InitAnnotation_JumpsBackToInitNode),
                           NavigationDirection.CSharpToNav,
                           """
                           Rücksprung von der BeginLogic landet auf dem `init Init1`-Knoten im .nav —
                           der Golden pinnt den exakten Span, nicht bloß irgendein Init1-Vorkommen.
                           """);
    }

    [Test]
    public void ChildInitAnnotation_JumpsBackToChildInitNode() {

        var ctx = CodeAnalysisTestContext.FromNav(InitFixtures.InitFlow);

        var annotation = ctx.InitAnnotation("Begin");

        var location = LocationFinder.FindNavLocationsAsync(ctx.NavSource, annotation, CancellationToken.None)
                                     .GetAwaiter().GetResult()
                                     .Single();

        GoldenAssert.Match(location, ctx, nameof(ChildInitAnnotation_JumpsBackToChildInitNode),
                           NavigationDirection.CSharpToNav,
                           "Rücksprung von der BeginLogic des Sub-Tasks landet auf dessen eigenem `init Begin`-Knoten.");
    }

    [Test]
    public void MissingInitAnnotation_ThrowsLocationNotFound() {

        // Negativpfad C#→Nav (innerer „initNode == null"-Zweig): der Task InitFlow existiert im .nav, der
        // annotierte Init-Name aber nicht. Der LocationFinder muss den fehlenden Init-Knoten als
        // LocationNotFoundException melden.
        var ctx  = CodeAnalysisTestContext.FromNav(InitFixtures.InitFlow);
        var real = ctx.InitAnnotation("Init1");

        var missing = new NavInitAnnotation(real, real.MethodDeclarationSyntax, initName: "GhostInit");

        Assert.That(
            () => LocationFinder.FindNavLocationsAsync(ctx.NavSource, missing, CancellationToken.None)
                                .GetAwaiter().GetResult(),
            Throws.TypeOf<LocationNotFoundException>());
    }
}
