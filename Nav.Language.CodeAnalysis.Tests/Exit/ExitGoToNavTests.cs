#region Using Directives

using System.Linq;
using System.Threading;

using NUnit.Framework;

using Pharmatechnik.Nav.Language.CodeAnalysis.FindSymbols;

#endregion

namespace Nav.Language.CodeAnalysis.Tests.Exit;

/// <summary>
/// C# → Nav: der Rücksprung von der generierten <c>After{TaskNode}Logic</c> zurück auf die Exit-Punkte
/// im <c>.nav</c>. Weil eine <c>AfterSubLogic</c> alle Exit-Punkte des Task-Knotens bündelt, ist der
/// Rücksprung MEHRDEUTIG: der <c>LocationFinder</c> liefert je Exit-Punkt eine
/// <see cref="AmbiguousLocation"/> (hier Sub:E1 und Sub:E2).
/// </summary>
[TestFixture]
public class ExitGoToNavTests {

    [Test]
    public void ExitAnnotation_JumpsToAllExitPoints() {

        var ctx = CodeAnalysisTestContext.FromNav(ExitFixtures.ExitFlow);

        var annotation = ctx.ExitAnnotation("Sub");

        var locations = LocationFinder.FindNavLocationsAsync(ctx.NavSource, annotation, CancellationToken.None)
                                      .GetAwaiter().GetResult()
                                      .ToList();

        Assert.That(locations, Has.Count.EqualTo(2),
                    "Erwartet: beide Exit-Punkte (Sub:E1, Sub:E2) als mehrdeutige Rücksprungziele.");

        GoldenAssert.Match(locations, ctx, nameof(ExitAnnotation_JumpsToAllExitPoints),
                           NavigationDirection.CSharpToNav,
                           """
                           Rücksprung von AfterSubLogic ist mehrdeutig und landet auf BEIDEN Exit-Punkten
                           Sub:E1 und Sub:E2 — der Golden pinnt beide Spans inkl. [ambiguous:<Name>].
                           """);
    }
}
