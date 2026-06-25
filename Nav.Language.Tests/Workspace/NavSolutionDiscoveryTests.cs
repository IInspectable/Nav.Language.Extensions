#region Using Directives

using System;
using System.IO;
using System.Linq;
using System.Threading;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;

#endregion

namespace Nav.Language.Tests.Workspace;

/// <summary>
/// Regressionsschutz gegen den Windows-Fallstrick, dass <c>Directory.EnumerateFiles(dir, "*.nav")</c> bei
/// einer 3-Zeichen-Endung auch Dateien mit längerer, gleich beginnender Endung liefert (z.B. <c>.navignore</c>).
/// </summary>
[TestFixture]
public class NavSolutionDiscoveryTests {

    string _root = null!;

    [SetUp]
    public void SetUp() {
        _root = Path.Combine(Path.GetTempPath(), "navsolution-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown() {
        try {
            Directory.Delete(_root, recursive: true);
        } catch (IOException) {
        }
    }

    [Test]
    public void HasNavExtension_OnlyExactNavExtension() {
        Assert.That(NavSolution.HasNavExtension(@"d:\x\a.nav"),      Is.True);
        Assert.That(NavSolution.HasNavExtension(@"d:\x\A.NAV"),      Is.True);
        Assert.That(NavSolution.HasNavExtension(@"d:\x\.navignore"), Is.False);
        Assert.That(NavSolution.HasNavExtension(@"d:\x\a.navx"),     Is.False);
        Assert.That(NavSolution.HasNavExtension(@"d:\x\a.txt"),      Is.False);
    }

    [Test]
    public void FromDirectory_ExcludesNavIgnoreAndNavLikeExtensions() {

        File.WriteAllText(Path.Combine(_root, "a.nav"),      "// leer\n");
        File.WriteAllText(Path.Combine(_root, ".navignore"), "*.nav\n");
        File.WriteAllText(Path.Combine(_root, "b.navdata"),  "egal\n");

        var solution = NavSolution.FromDirectoryAsync(new DirectoryInfo(_root), CancellationToken.None).Result;

        var names = solution.SolutionFiles.Select(f => f.Name).ToArray();

        Assert.That(names, Is.EquivalentTo(new[] { "a.nav" }));
    }

}