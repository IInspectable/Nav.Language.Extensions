#region Using Directives

using System.Linq;
using System.Threading;

using NUnit.Framework;

using Pharmatechnik.Nav.Language.CodeAnalysis.Annotation;
using Pharmatechnik.Nav.Language.CodeAnalysis.FindSymbols;

#endregion

namespace Nav.Language.CodeAnalysis.Tests.Choice;

/// <summary>
/// C# → Nav: der Rücksprung von der generierten <c>{Choice}Logic</c> bzw. von einer
/// <c>next.{Choice}(…)</c>-Aufrufstelle zurück auf den <c>choice X</c>-Knoten im <c>.nav</c>. Die
/// Annotation wird dabei über den echten <c>AnnotationReader</c> aus dem generierten Code gelesen.
/// </summary>
[TestFixture]
public class ChoiceGoToNavTests {

    [Test]
    public void ChoiceLogicAnnotation_JumpsBackToChoiceNode() {

        var ctx = CodeAnalysisTestContext.FromNav(ChoiceFixtures.ChoiceFlow);

        var annotation = ctx.ChoiceAnnotation("Choice_Retry");

        var location = LocationFinder.FindNavLocationsAsync(ctx.NavSource, annotation, CancellationToken.None)
                                     .GetAwaiter().GetResult()
                                     .Single();

        GoldenAssert.Match(location, ctx, nameof(ChoiceLogicAnnotation_JumpsBackToChoiceNode),
                           NavigationDirection.CSharpToNav,
                           """
                           Rücksprung von Choice_RetryLogic landet auf der `choice Choice_Retry`-Deklaration im .nav —
                           der Golden pinnt den exakten Span, nicht bloß irgendein Choice_Retry-Vorkommen.
                           """);
    }

    [Test]
    public void ChoiceCallAnnotation_JumpsBackToChoiceNode() {

        var ctx = CodeAnalysisTestContext.FromNav(ChoiceFixtures.ChoiceFlow, ChoiceFixtures.ChoiceFlowUserCode);

        var annotation = ctx.ChoiceCallAnnotation("Choice_Retry");

        var location = LocationFinder.FindNavLocationsAsync(ctx.NavSource, annotation, CancellationToken.None)
                                     .GetAwaiter().GetResult()
                                     .Single();

        GoldenAssert.Match(location, ctx, nameof(ChoiceCallAnnotation_JumpsBackToChoiceNode),
                           NavigationDirection.CSharpToNav,
                           "GoTo direkt auf next.Choice_Retry(…) landet ebenfalls auf dem `choice Choice_Retry`-Knoten.");
    }

    [Test]
    public void SecondChoiceLogicAnnotation_JumpsBackToItsNode() {

        var ctx = CodeAnalysisTestContext.FromNav(ChoiceFixtures.ChoiceFlow);

        var annotation = ctx.ChoiceAnnotation("Choice_Escalate");

        var location = LocationFinder.FindNavLocationsAsync(ctx.NavSource, annotation, CancellationToken.None)
                                     .GetAwaiter().GetResult()
                                     .Single();

        GoldenAssert.Match(location, ctx, nameof(SecondChoiceLogicAnnotation_JumpsBackToItsNode),
                           NavigationDirection.CSharpToNav,
                           "Rücksprung von Choice_EscalateLogic landet auf dem eigenen `choice Choice_Escalate`-Knoten.");
    }

    [Test]
    public void EscalateCallAnnotation_JumpsBackToChoiceNode() {

        var ctx = CodeAnalysisTestContext.FromNav(ChoiceFixtures.ChoiceFlow, ChoiceFixtures.ChoiceFlowUserCode);

        var annotation = ctx.ChoiceCallAnnotation("Choice_Escalate");

        var location = LocationFinder.FindNavLocationsAsync(ctx.NavSource, annotation, CancellationToken.None)
                                     .GetAwaiter().GetResult()
                                     .Single();

        GoldenAssert.Match(location, ctx, nameof(EscalateCallAnnotation_JumpsBackToChoiceNode),
                           NavigationDirection.CSharpToNav,
                           "GoTo direkt auf next.Choice_Escalate(…) (Choice→Choice) landet auf dem `choice Choice_Escalate`-Knoten.");
    }

    [Test]
    public void MissingChoiceAnnotation_ThrowsLocationNotFound() {

        // Negativpfad C#→Nav (innerer „choiceNode == null"-Zweig): der Task ChoiceFlow existiert im .nav, der
        // annotierte Choice-Name aber nicht. Der LocationFinder muss den fehlenden Choice-Knoten als
        // LocationNotFoundException melden.
        var ctx  = CodeAnalysisTestContext.FromNav(ChoiceFixtures.ChoiceFlow);
        var real = ctx.ChoiceAnnotation("Choice_Retry");

        var missing = new NavChoiceAnnotation(real, real.MethodDeclarationSyntax, choiceName: "Choice_Ghost");

        Assert.That(
            () => LocationFinder.FindNavLocationsAsync(ctx.NavSource, missing, CancellationToken.None)
                                .GetAwaiter().GetResult(),
            Throws.TypeOf<LocationNotFoundException>());
    }
}
