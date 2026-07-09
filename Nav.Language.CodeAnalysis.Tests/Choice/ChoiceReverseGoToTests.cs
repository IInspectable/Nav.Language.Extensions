#region Using Directives

using System.Linq;
using System.Threading;

using NUnit.Framework;

using Pharmatechnik.Nav.Language.CodeAnalysis.FindSymbols;

#endregion

namespace Nav.Language.CodeAnalysis.Tests.Choice;

/// <summary>
/// C# → Nav: der Rücksprung von der generierten <c>{Choice}Logic</c> bzw. von einer
/// <c>next.{Choice}(…)</c>-Aufrufstelle zurück auf den <c>choice X</c>-Knoten im <c>.nav</c>. Die
/// Annotation wird dabei über den echten <c>AnnotationReader</c> aus dem generierten Code gelesen.
/// </summary>
[TestFixture]
public class ChoiceReverseGoToTests {

    [Test]
    public void ChoiceLogicAnnotation_JumpsBackToChoiceNode() {

        var ctx = CodeAnalysisTestContext.FromNav(ChoiceFixtures.ChoiceFlow);

        var annotation = ctx.ChoiceAnnotation("Choice_Retry");

        var location = LocationFinder.FindNavLocationsAsync(ctx.NavSource, annotation, CancellationToken.None)
                                     .GetAwaiter().GetResult()
                                     .Single();

        // Intra-Text-GoTo auf Choice_RetryLogic landet auf `choice Choice_Retry` im .nav.
        Assert.That(ctx.TextAt(location), Is.EqualTo("Choice_Retry"));
        Assert.That(ctx.IsInNav(location), Is.True);
    }

    [Test]
    public void ChoiceCallAnnotation_JumpsBackToChoiceNode() {

        var ctx = CodeAnalysisTestContext.FromNav(ChoiceFixtures.ChoiceFlow, ChoiceFixtures.ChoiceFlowUserCode);

        var annotation = ctx.ChoiceCallAnnotation("Choice_Retry");

        var location = LocationFinder.FindNavLocationsAsync(ctx.NavSource, annotation, CancellationToken.None)
                                     .GetAwaiter().GetResult()
                                     .Single();

        // GoTo direkt auf next.Choice_Retry(…) landet ebenfalls auf dem Choice-Knoten.
        Assert.That(ctx.TextAt(location),  Is.EqualTo("Choice_Retry"));
        Assert.That(ctx.IsInNav(location), Is.True);
    }

    [Test]
    public void SecondChoiceLogicAnnotation_JumpsBackToItsNode() {

        var ctx = CodeAnalysisTestContext.FromNav(ChoiceFixtures.ChoiceFlow);

        var annotation = ctx.ChoiceAnnotation("Choice_Escalate");

        var location = LocationFinder.FindNavLocationsAsync(ctx.NavSource, annotation, CancellationToken.None)
                                     .GetAwaiter().GetResult()
                                     .Single();

        Assert.That(ctx.TextAt(location),  Is.EqualTo("Choice_Escalate"));
        Assert.That(ctx.IsInNav(location), Is.True);
    }
}
