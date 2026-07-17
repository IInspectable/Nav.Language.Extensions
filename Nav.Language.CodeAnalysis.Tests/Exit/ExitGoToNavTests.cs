#region Using Directives

using System.Linq;
using System.Threading;

using NUnit.Framework;

using Pharmatechnik.Nav.Language.CodeAnalysis.Annotation;
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

    [Test]
    public void MissingExitAnnotation_ThrowsLocationNotFound() {

        // Negativpfad C#→Nav (innerer „keine Exit-Transitions"-Zweig): der Task ExitFlow existiert im .nav,
        // aber kein Task-Knoten mit dem annotierten Namen — es gibt also keine passenden Exit-Transitions.
        // Der LocationFinder muss das als LocationNotFoundException melden.
        var ctx  = CodeAnalysisTestContext.FromNav(ExitFixtures.ExitFlow);
        var real = ctx.ExitAnnotation("Sub");

        var missing = new NavExitAnnotation(real, real.MethodDeclarationSyntax, exitTaskName: "GhostNode");

        Assert.That(
            () => LocationFinder.FindNavLocationsAsync(ctx.NavSource, missing, CancellationToken.None)
                                .GetAwaiter().GetResult(),
            Throws.TypeOf<LocationNotFoundException>());
    }
}
