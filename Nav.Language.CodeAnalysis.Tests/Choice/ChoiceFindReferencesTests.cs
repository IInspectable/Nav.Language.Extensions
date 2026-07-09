#region Using Directives

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
        // Der Golden hält jede Referenz mit Datei + exaktem Span — verschobene, fehlende oder
        // zusätzliche Treffer fallen auf (nicht nur die Anzahl).
        GoldenAssert.Match(NavigationSnapshot.Serialize(references, ctx), nameof(ChoiceRetry_FindsAllThreeForwardCallSites));
    }

    [Test]
    public void ChoiceEscalate_FindsItsSingleForwardCallSite() {

        var ctx = CodeAnalysisTestContext.FromNav(ChoiceFixtures.ChoiceFlow);

        var references = ctx.FindChoiceReferences("Choice_Escalate");

        // Eine Quelle (Choice_Retry --> Choice_Escalate) forwardet an Choice_Escalate — plus das
        // nameof(Choice_EscalateLogic) im Unwrap(). Alle Verweise zeigen auf Choice_EscalateLogic.
        GoldenAssert.Match(NavigationSnapshot.Serialize(references, ctx), nameof(ChoiceEscalate_FindsItsSingleForwardCallSite));
    }
}
