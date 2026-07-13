#region Using Directives

using System;
using System.IO;
using System.Linq;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;

#endregion

namespace Nav.Language.Tests;

/// <summary>
/// Tests für den <see cref="CachedSemanticModelProvider"/> (Tier 2): Treffer liefern referenzgleiche
/// Units; die Invalidierung läuft ausschließlich über die Selbstvalidierung gegen die
/// Syntax-Instanzen von Tier 1 (Primärdatei + direkte Includes, Vorwärts-Snapshot). Die Datei-Änderungen
/// erfolgen out-of-band auf der Platte — bemerkt werden sie über die Stempel-Frische des
/// <see cref="OverlaySyntaxProvider"/>, ohne expliziten Invalidate-Aufruf.
/// </summary>
[TestFixture]
public class CachedSemanticModelProviderTests {

    const string LibSource =
        """
        task Sub
        {
            init i;
            exit x;
            i --> x;
        }

        """;

    const string LibWithSub2Source =
        """
        task Sub
        {
            init i;
            exit x;
            i --> x;
        }

        task Sub2
        {
            init i;
            exit x;
            i --> x;
        }

        """;

    static string Consumer(string taskName, string includeFile, string referencedTask) =>
        $$"""
          taskref "{{includeFile}}";

          task {{taskName}}
          {
              init i;
              task {{referencedTask}} s;
              exit e;
              i   --> s;
              s:x --> e;
          }

          """;

    [Test]
    public void SecondLookup_ReturnsSameUnitInstance() {

        using var tmp = new TempDir();
        tmp.Write("lib.nav", LibSource);
        tmp.Write("a.nav",   Consumer("A", "lib.nav", "Sub"));

        var provider = CreateProvider();

        var unitA1   = provider.GetSemanticModel(tmp.Path("a.nav"));
        var unitLib1 = provider.GetSemanticModel(tmp.Path("lib.nav"));

        var unitA2   = provider.GetSemanticModel(tmp.Path("a.nav"));
        var unitLib2 = provider.GetSemanticModel(tmp.Path("lib.nav"));

        Assert.That(unitA2,   Is.SameAs(unitA1));
        Assert.That(unitLib2, Is.SameAs(unitLib1));
    }

    [Test]
    public void ChangedPrimaryFile_IsRebuilt_NeighborsStayCached() {

        using var tmp = new TempDir();
        tmp.Write("lib.nav", LibSource);
        tmp.Write("a.nav",   Consumer("A", "lib.nav", "Sub"));

        var provider = CreateProvider();

        var unitA1   = provider.GetSemanticModel(tmp.Path("a.nav"));
        var unitLib1 = provider.GetSemanticModel(tmp.Path("lib.nav"));

        // Out-of-band-Änderung der Primärdatei — kein Invalidate, nur die Platte.
        tmp.Write("a.nav", Consumer("ARenamed", "lib.nav", "Sub"));

        var unitA2   = provider.GetSemanticModel(tmp.Path("a.nav"));
        var unitLib2 = provider.GetSemanticModel(tmp.Path("lib.nav"));

        Assert.That(unitA2,                                      Is.Not.SameAs(unitA1));
        Assert.That(unitA2!.TaskDefinitions.Select(t => t.Name), Has.Member("ARenamed"));
        // Der Nachbar hängt nicht an a.nav und bleibt Treffer.
        Assert.That(unitLib2, Is.SameAs(unitLib1));
    }

    [Test]
    public void ChangedInclude_RebuildsIncludingFile_AndReflectsNewDeclarations() {

        using var tmp = new TempDir();
        tmp.Write("lib.nav", LibSource);
        tmp.Write("a.nav",   Consumer("A", "lib.nav", "Sub"));

        var provider = CreateProvider();

        var unitA1 = provider.GetSemanticModel(tmp.Path("a.nav"));

        // Das Include ändert sich auf der Platte — a.nav selbst bleibt unangetastet.
        tmp.Write("lib.nav", LibWithSub2Source);

        var unitA2 = provider.GetSemanticModel(tmp.Path("a.nav"));

        Assert.That(unitA2,                                                         Is.Not.SameAs(unitA1));
        Assert.That(unitA2!.Includes.Single().TaskDeclarations.Select(d => d.Name), Has.Member("Sub2"));
    }

    [Test]
    public void ChangedTransitiveInclude_DoesNotInvalidateIndirectConsumer() {

        using var tmp = new TempDir();
        // Kette: a inkludiert b, b inkludiert c — Includes wirken nicht transitiv,
        // a hängt also NICHT an c.
        tmp.Write("c.nav", LibSource);
        tmp.Write("b.nav", Consumer("B", "c.nav", "Sub"));
        tmp.Write("a.nav", Consumer("A", "b.nav", "B"));

        var provider = CreateProvider();

        var unitA1 = provider.GetSemanticModel(tmp.Path("a.nav"));
        var unitB1 = provider.GetSemanticModel(tmp.Path("b.nav"));

        tmp.Write("c.nav", LibWithSub2Source);

        var unitA2 = provider.GetSemanticModel(tmp.Path("a.nav"));
        var unitB2 = provider.GetSemanticModel(tmp.Path("b.nav"));

        // a bleibt Treffer (c ist kein direktes Include von a) …
        Assert.That(unitA2, Is.SameAs(unitA1));
        // … der direkte Konsument b wird neu gebaut.
        Assert.That(unitB2, Is.Not.SameAs(unitB1));
    }

    [Test]
    public void DeletedFile_ReturnsNull_AndReappearanceIsPickedUp() {

        using var tmp = new TempDir();
        tmp.Write("a.nav",   Consumer("A", "lib.nav", "Sub"));
        tmp.Write("lib.nav", LibSource);

        var provider = CreateProvider();

        Assert.That(provider.GetSemanticModel(tmp.Path("a.nav")), Is.Not.Null);

        File.Delete(tmp.Path("a.nav"));
        Assert.That(provider.GetSemanticModel(tmp.Path("a.nav")), Is.Null);

        tmp.Write("a.nav", Consumer("AReborn", "lib.nav", "Sub"));
        var reborn = provider.GetSemanticModel(tmp.Path("a.nav"));

        Assert.That(reborn,                                      Is.Not.Null);
        Assert.That(reborn!.TaskDefinitions.Select(t => t.Name), Has.Member("AReborn"));
    }

    [Test]
    public void SyntaxWithoutFile_IsNotCached_ButYieldsCorrectModel() {

        var provider = CreateProvider();

        // Ungespeicherter Puffer: Parse ohne Dateipfad → kein FileInfo, nicht pfad-adressierbar.
        var syntax = Syntax.ParseCodeGenerationUnit(
            """
            task X
            {
                init i;
                exit e;
                i --> e;
            }

            """);

        var unit1 = provider.GetSemanticModel(syntax);
        var unit2 = provider.GetSemanticModel(syntax);

        Assert.That(unit1.TaskDefinitions.Select(t => t.Name), Has.Member("X"));
        // Durchgereicht statt gecacht: jeder Aufruf baut neu.
        Assert.That(unit2, Is.Not.SameAs(unit1));
    }

    static CachedSemanticModelProvider CreateProvider() {
        var syntaxProvider = new OverlaySyntaxProvider();
        return new CachedSemanticModelProvider(new SemanticModelProvider(syntaxProvider), syntaxProvider);
    }

    /// <summary>
    /// Echte Dateien im Temp-Verzeichnis — nötig, weil die Include-Auflösung über das Dateisystem
    /// geht (relative Pfade werden gegen das Verzeichnis der Quelldatei aufgelöst).
    /// </summary>
    sealed class TempDir: IDisposable {

        readonly string _dir;

        public TempDir() {
            _dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "navsemcache_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public string Path(string fileName) => System.IO.Path.Combine(_dir, fileName);

        public void Write(string fileName, string content) => File.WriteAllText(Path(fileName), content);

        public void Dispose() {
            try {
                Directory.Delete(_dir, recursive: true);
            } catch {
                // Best effort — Temp-Aufräumen darf den Testlauf nicht zum Scheitern bringen.
            }
        }

    }

}
