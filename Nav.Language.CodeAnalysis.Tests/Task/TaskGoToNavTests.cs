#region Using Directives

using System.Linq;
using System.Threading;

using NUnit.Framework;

using Pharmatechnik.Nav.Language.CodeAnalysis.FindSymbols;

#endregion

namespace Nav.Language.CodeAnalysis.Tests.Task;

/// <summary>
/// C# → Nav: der Rücksprung von der generierten <c>{Task}WFS(Base)</c> zurück auf die <c>task X</c>-
/// Deklaration im <c>.nav</c>. Die <c>&lt;NavTask&gt;</c>-Annotation wird dabei über den echten
/// <c>AnnotationReader</c> aus dem generierten Code gelesen.
/// </summary>
[TestFixture]
public class TaskGoToNavTests {

    [Test]
    public void TaskAnnotation_JumpsBackToTaskDeclaration() {

        var ctx = CodeAnalysisTestContext.FromNav(TaskFixtures.TaskFlow);

        var annotation = ctx.TaskAnnotation("TaskFlow");

        var location = LocationFinder.FindNavLocationsAsync(ctx.NavSource, annotation, CancellationToken.None)
                                     .GetAwaiter().GetResult()
                                     .Single();

        GoldenAssert.Match(location, ctx, nameof(TaskAnnotation_JumpsBackToTaskDeclaration),
                           NavigationDirection.CSharpToNav,
                           """
                           Rücksprung aus der TaskFlowWFS landet auf der `task TaskFlow`-Deklaration im .nav —
                           der Golden pinnt den exakten Span, nicht bloß irgendein TaskFlow-Vorkommen.
                           """);
    }

    [Test]
    public void SecondTaskAnnotation_JumpsBackToItsDeclaration() {

        var ctx = CodeAnalysisTestContext.FromNav(TaskFixtures.TaskFlow);

        var annotation = ctx.TaskAnnotation("Helper");

        var location = LocationFinder.FindNavLocationsAsync(ctx.NavSource, annotation, CancellationToken.None)
                                     .GetAwaiter().GetResult()
                                     .Single();

        GoldenAssert.Match(location, ctx, nameof(SecondTaskAnnotation_JumpsBackToItsDeclaration),
                           NavigationDirection.CSharpToNav,
                           "Rücksprung aus der HelperWFS landet auf der eigenen `task Helper`-Deklaration.");
    }
}
