#region Using Directives

using System;
using System.IO;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;

#endregion

namespace Nav.Language.Tests.Workspace;

/// <summary>
/// Hierarchisches Verhalten von <see cref="NavIgnore"/> über echte temporäre Verzeichnisbäume:
/// tiefere .navignore schlägt flachere, und <see cref="NavIgnore.Load"/> (Scangrenze = Wurzel) verhält sich
/// anders als <see cref="NavIgnore.LoadForAncestors"/> (git-artiger Aufwärts-Walk).
/// </summary>
[TestFixture]
public class NavIgnoreHierarchyTests {

    string _root = null!;

    [SetUp]
    public void SetUp() {

        _root = Path.Combine(Path.GetTempPath(), "navignore-tests", Guid.NewGuid().ToString("N"));

        // work/
        //   .navignore        -> "*.nav"
        //   a.nav
        //   sub/
        //     .navignore      -> "!important.nav"
        //     important.nav
        //     other.nav
        Directory.CreateDirectory(Path.Combine(_root, "sub"));

        File.WriteAllText(Path.Combine(_root, ".navignore"), "*.nav\n");
        File.WriteAllText(Path.Combine(_root, "a.nav"),      "// leer\n");

        File.WriteAllText(Path.Combine(_root, "sub", ".navignore"),    "!important.nav\n");
        File.WriteAllText(Path.Combine(_root, "sub", "important.nav"), "// leer\n");
        File.WriteAllText(Path.Combine(_root, "sub", "other.nav"),     "// leer\n");
    }

    [TearDown]
    public void TearDown() {
        try {
            Directory.Delete(_root, recursive: true);
        } catch (IOException) {
        }
    }

    string P(params string[] parts) => Path.Combine(_root, Path.Combine(parts));

    [Test]
    public void Load_AppliesRootRulesToSubtree_AndDeeperNegationWins() {

        var ignore = NavIgnore.Load(_root);

        Assert.That(ignore.IsIgnored(P("a.nav")),                Is.True,  "Wurzel-Regel *.nav greift");
        Assert.That(ignore.IsIgnored(P("sub", "other.nav")),     Is.True,  "Wurzel-Regel greift auch im Unterbaum");
        Assert.That(ignore.IsIgnored(P("sub", "important.nav")), Is.False, "tiefere Negation !important.nav schlägt die Wurzel-Regel");
    }

    [Test]
    public void Load_FromSubdirectory_DoesNotSeeParentNavIgnore() {

        // Scangrenze = sub: nur sub/.navignore (!important.nav) gilt; die Eltern-.navignore (*.nav) wird NICHT gesehen.
        var ignore = NavIgnore.Load(Path.Combine(_root, "sub"));

        Assert.That(ignore.IsIgnored(P("sub", "other.nav")),     Is.False, "kein Eltern-*.nav → nicht ignoriert");
        Assert.That(ignore.IsIgnored(P("sub", "important.nav")), Is.False);
    }

    [Test]
    public void LoadForAncestors_SeesParentNavIgnore_WithDeeperNegation() {

        // Aufwärts-Walk ab sub: sub/.navignore UND die Eltern-.navignore (*.nav) gelten.
        var ignore = NavIgnore.LoadForAncestors(Path.Combine(_root, "sub"));

        Assert.That(ignore.IsIgnored(P("sub", "other.nav")),     Is.True,  "Eltern-Regel *.nav greift über den Aufwärts-Walk");
        Assert.That(ignore.IsIgnored(P("sub", "important.nav")), Is.False, "tiefere Negation gewinnt weiterhin");
        Assert.That(ignore.IsIgnored(P("a.nav")),                Is.True,  "Datei direkt im Eltern-Verzeichnis");
    }

    [Test]
    public void Empty_WhenNoNavIgnorePresent() {

        var bare = Path.Combine(_root, "bare");
        Directory.CreateDirectory(bare);

        var ignore = NavIgnore.Load(bare);

        Assert.That(ignore.IsIgnored(Path.Combine(bare, "x.nav")), Is.False);
    }

}