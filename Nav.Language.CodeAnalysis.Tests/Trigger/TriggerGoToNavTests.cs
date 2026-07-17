#region Using Directives

using System.Linq;
using System.Threading;

using NUnit.Framework;

using Pharmatechnik.Nav.Language.CodeAnalysis.Annotation;
using Pharmatechnik.Nav.Language.CodeAnalysis.FindSymbols;

#endregion

namespace Nav.Language.CodeAnalysis.Tests.Trigger;

/// <summary>
/// C# → Nav: der Rücksprung von der generierten <c>{Trigger}Logic</c> zurück auf den Signal-Trigger
/// (<c>… on OnX</c>) im <c>.nav</c>. Die <c>&lt;NavTrigger&gt;</c>-Annotation wird dabei über den echten
/// <c>AnnotationReader</c> aus dem generierten Code gelesen.
/// </summary>
[TestFixture]
public class TriggerGoToNavTests {

    [Test]
    public void TriggerAnnotation_JumpsBackToTrigger() {

        var ctx = CodeAnalysisTestContext.FromNav(TriggerFixtures.TriggerFlow);

        var annotation = ctx.TriggerAnnotation("OnOpen");

        var location = LocationFinder.FindNavLocationsAsync(ctx.NavSource, annotation, CancellationToken.None)
                                     .GetAwaiter().GetResult()
                                     .Single();

        GoldenAssert.Match(location, ctx, nameof(TriggerAnnotation_JumpsBackToTrigger),
                           NavigationDirection.CSharpToNav,
                           """
                           Rücksprung von OnOpenLogic landet auf dem Trigger `on OnOpen` im .nav —
                           der Golden pinnt den exakten Span, nicht bloß irgendein OnOpen-Vorkommen.
                           """);
    }

    [Test]
    public void SecondTriggerAnnotation_JumpsBackToItsTrigger() {

        var ctx = CodeAnalysisTestContext.FromNav(TriggerFixtures.TriggerFlow);

        var annotation = ctx.TriggerAnnotation("OnClose");

        var location = LocationFinder.FindNavLocationsAsync(ctx.NavSource, annotation, CancellationToken.None)
                                     .GetAwaiter().GetResult()
                                     .Single();

        GoldenAssert.Match(location, ctx, nameof(SecondTriggerAnnotation_JumpsBackToItsTrigger),
                           NavigationDirection.CSharpToNav,
                           "Rücksprung von OnCloseLogic landet auf dem eigenen Trigger `on OnClose`.");
    }

    [Test]
    public void MissingTriggerAnnotation_ThrowsLocationNotFound() {

        // Negativpfad C#→Nav (innerer „trigger == null"-Zweig): der Task TriggerFlow existiert im .nav, der
        // annotierte Trigger-Name aber nicht. Der LocationFinder muss den fehlenden Trigger als
        // LocationNotFoundException melden.
        var ctx  = CodeAnalysisTestContext.FromNav(TriggerFixtures.TriggerFlow);
        var real = ctx.TriggerAnnotation("OnOpen");

        var missing = new NavTriggerAnnotation(real, real.MethodDeclarationSyntax, triggerName: "OnGhost");

        Assert.That(
            () => LocationFinder.FindNavLocationsAsync(ctx.NavSource, missing, CancellationToken.None)
                                .GetAwaiter().GetResult(),
            Throws.TypeOf<LocationNotFoundException>());
    }
}
