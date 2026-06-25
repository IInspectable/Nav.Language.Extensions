#region Using Directives

using NUnit.Framework;

using Pharmatechnik.Nav.Language;

#endregion

namespace Nav.Language.Tests.Workspace;

/// <summary>
/// Muster- und Datei-Ebene des .navignore-Matchers (ohne Datei-IO über <see cref="NavIgnoreFile.FromLines"/>).
/// Die übergebenen relativen Pfade sind — wie zur Laufzeit — bereits kleingeschrieben und mit '/' getrennt.
/// </summary>
[TestFixture]
public class NavIgnoreTests {

    const string BaseDir = @"d:\nav";

    static bool? Match(string relativePathPosix, params string[] lines) {
        var file = NavIgnoreFile.FromLines(BaseDir, lines);
        return file.Match(relativePathPosix);
    }

    [Test]
    public void CommentsAndBlankLines_ProduceNoRules() {
        Assert.That(Match("foo.nav", "# ein Kommentar", "", "   "), Is.Null);
    }

    [Test]
    public void FloatingName_MatchesAtAnyDepth() {
        Assert.That(Match("foo.nav",     "foo.nav"), Is.True);
        Assert.That(Match("sub/foo.nav", "foo.nav"), Is.True);
        Assert.That(Match("a/b/foo.nav", "foo.nav"), Is.True);
        Assert.That(Match("bar.nav",     "foo.nav"), Is.Null);
    }

    [Test]
    public void AnchoredName_MatchesOnlyAtRoot() {
        Assert.That(Match("foo.nav",     "/foo.nav"), Is.True);
        Assert.That(Match("sub/foo.nav", "/foo.nav"), Is.Null);
    }

    [Test]
    public void Star_DoesNotCrossDirectorySeparator() {
        // Anker durch den Separator: sub/*.nav greift nur direkt unter sub.
        Assert.That(Match("sub/b.nav",      "sub/*.nav"), Is.True);
        Assert.That(Match("sub/deep/b.nav", "sub/*.nav"), Is.Null);
    }

    [Test]
    public void Star_FloatingMatchesEachLevel() {
        Assert.That(Match("a.nav",     "*.nav"), Is.True);
        Assert.That(Match("sub/b.nav", "*.nav"), Is.True);
    }

    [Test]
    public void DoubleStar_CrossesDirectories() {
        Assert.That(Match("b.nav",     "**/b.nav"), Is.True);
        Assert.That(Match("x/y/b.nav", "**/b.nav"), Is.True);

        Assert.That(Match("a/b.nav",     "a/**"), Is.True);
        Assert.That(Match("a/x/y.nav",   "a/**"), Is.True);
        Assert.That(Match("other/b.nav", "a/**"), Is.Null);
    }

    [Test]
    public void QuestionMark_MatchesSingleChar() {
        Assert.That(Match("a.nav",  "?.nav"), Is.True);
        Assert.That(Match("ab.nav", "?.nav"), Is.Null);
    }

    [Test]
    public void DirectoryOnlyPattern_MatchesSubtree() {
        Assert.That(Match("build/x.nav",   "build/"), Is.True);
        Assert.That(Match("a/build/x.nav", "build/"), Is.True);
        // Eine Datei, die nur so heißt wie das Verzeichnis, greift nicht (Verzeichnis-Muster).
        Assert.That(Match("build", "build/"), Is.Null);
    }

    [Test]
    public void Negation_ReincludesAfterIgnore() {
        Assert.That(Match("a.nav",    "*.nav", "!keep.nav"), Is.True);
        Assert.That(Match("keep.nav", "*.nav", "!keep.nav"), Is.False);
    }

    [Test]
    public void LastMatchWins_OrderMatters() {
        // Umgekehrte Reihenfolge: erst negiert, dann wieder ignoriert → ignoriert.
        Assert.That(Match("keep.nav", "!keep.nav", "*.nav"), Is.True);
    }

    [Test]
    public void Matching_IsCaseInsensitive() {
        // Das Muster wird intern kleingeschrieben; der Pfad kommt zur Laufzeit bereits kleingeschrieben.
        Assert.That(Match("foo.nav", "Foo.NAV"), Is.True);
    }

}