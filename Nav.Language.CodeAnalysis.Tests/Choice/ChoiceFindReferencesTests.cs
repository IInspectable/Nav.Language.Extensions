#region Using Directives

using System.Linq;

using NUnit.Framework;

#endregion

namespace Nav.Language.CodeAnalysis.Tests.Choice;

/// <summary>
/// „Alle Referenzen" auf eine Choice: der <see cref="Pharmatechnik.Nav.Language.CodeAnalysis.FindReferences.WfsReferenceFinder"/>
/// findet über den Roslyn-Workspace die generierten <c>{Choice}(…)</c>-Forward-Aufrufstellen, die auf
/// die abstrakte <c>{Choice}Logic</c> verweisen.
/// </summary>
[TestFixture]
public class ChoiceFindReferencesTests {

    [Test]
    public void ChoiceRetry_FindsAllThreeForwardCallSites() {

        var ctx = CodeAnalysisTestContext.FromNav(ChoiceFixtures.ChoiceFlow);

        var references = ctx.FindChoiceReferences("Choice_Retry");

        // Alle C#-Verweise auf Choice_RetryLogic: die drei Forward-Aufrufstellen (Init/Trigger/Exit
        // delegieren über _wfs.Choice_RetryLogic(…)) plus das nameof(Choice_RetryLogic) im Unwrap().
        Assert.That(references.Count, Is.EqualTo(4));
        Assert.That(references.Select(r => ctx.TextAt(r.Location)),
                    Is.All.EqualTo("Choice_RetryLogic"));
    }

    [Test]
    public void ChoiceEscalate_FindsItsSingleForwardCallSite() {

        var ctx = CodeAnalysisTestContext.FromNav(ChoiceFixtures.ChoiceFlow);

        var references = ctx.FindChoiceReferences("Choice_Escalate");

        // Eine Quelle (Choice_Retry --> Choice_Escalate) forwardet an Choice_Escalate — plus das
        // nameof(Choice_EscalateLogic) im Unwrap(). Alle Verweise zeigen auf Choice_EscalateLogic.
        Assert.That(references.Count, Is.EqualTo(2));
        Assert.That(references.Select(r => ctx.TextAt(r.Location)),
                    Is.All.EqualTo("Choice_EscalateLogic"));
    }
}
