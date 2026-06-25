#region Using Directives

using System.Linq;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Utilities.IO;

#endregion

namespace Nav.Language.Tests.Workspace;

[TestFixture]
public class IncludeDependencyGraphTests {

    const string A = @"n:\nav\a.nav";
    const string B = @"n:\nav\b.nav";
    const string C = @"n:\nav\c.nav";
    const string D = @"n:\nav\d.nav";

    static string Key(string path) => PathHelper.NormalizePath(path);

    [Test]
    public void DirectIncluder_IsReturned() {

        var graph = new IncludeDependencyGraph();
        graph.SetIncludes(B, new[] { A }); // b inkludiert a

        var dependents = graph.GetDependentsClosure(A);

        Assert.That(dependents, Is.EquivalentTo(new[] { Key(B) }));
    }

    [Test]
    public void TransitiveChain_AllIncludersReturned() {

        var graph = new IncludeDependencyGraph();
        graph.SetIncludes(B, new[] { A }); // b -> a
        graph.SetIncludes(C, new[] { B }); // c -> b

        var dependents = graph.GetDependentsClosure(A);

        Assert.That(dependents, Is.EquivalentTo(new[] { Key(B), Key(C) }));
    }

    [Test]
    public void Diamond_EachIncluderReturnedOnce() {

        var graph = new IncludeDependencyGraph();
        graph.SetIncludes(B, new[] { A });      // b -> a
        graph.SetIncludes(C, new[] { A });      // c -> a
        graph.SetIncludes(D, new[] { B, C });   // d -> b, c

        var dependents = graph.GetDependentsClosure(A);

        Assert.That(dependents, Is.EquivalentTo(new[] { Key(B), Key(C), Key(D) }));
    }

    [Test]
    public void Cycle_TerminatesAndExcludesStart() {

        var graph = new IncludeDependencyGraph();
        graph.SetIncludes(A, new[] { B }); // a -> b
        graph.SetIncludes(B, new[] { A }); // b -> a  (Zyklus)

        var dependents = graph.GetDependentsClosure(A);

        // Kein Endlos-BFS; a (der Startknoten) ist nicht enthalten, b schon.
        Assert.That(dependents, Is.EquivalentTo(new[] { Key(B) }));
    }

    [Test]
    public void SelfInclude_IsIgnored() {

        var graph = new IncludeDependencyGraph();
        graph.SetIncludes(A, new[] { A }); // a inkludiert sich selbst — wird ignoriert

        Assert.That(graph.GetDependentsClosure(A), Is.Empty);
    }

    [Test]
    public void UnknownFile_ReturnsEmpty() {

        var graph = new IncludeDependencyGraph();
        graph.SetIncludes(B, new[] { A });

        Assert.That(graph.GetDependentsClosure(C), Is.Empty);
    }

    [Test]
    public void SetIncludes_ReplacesPreviousEdges() {

        var graph = new IncludeDependencyGraph();
        graph.SetIncludes(C, new[] { A }); // c -> a
        graph.SetIncludes(C, new[] { B }); // c inkludiert nun b statt a

        Assert.That(graph.GetDependentsClosure(A), Is.Empty);
        Assert.That(graph.GetDependentsClosure(B), Is.EquivalentTo(new[] { Key(C) }));
    }
}
