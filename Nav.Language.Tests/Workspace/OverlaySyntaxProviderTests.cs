#region Using Directives

using System;
using System.IO;
using System.Threading;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Utilities.IO;

#endregion

namespace Nav.Language.Tests.Workspace;

/// <summary>
/// Stempel-Frische des <see cref="OverlaySyntaxProvider"/>: out-of-band geänderte Dateien werden beim
/// nächsten Zugriff ohne explizite Invalidierung bemerkt (Poll beim Lesen statt Watcher); Overlays
/// bleiben autoritativ und stempeln nicht gegen die Platte.
/// </summary>
[TestFixture]
public class OverlaySyntaxProviderTests {

    const string AlphaSource =
        """
        task Alpha
        {
            init I1;
            exit End;
            I1 --> End;
        }

        """;

    const string BetaSource =
        """
        task Beta
        {
            init I1;
            exit End;

            I1 --> End;
        }

        """;

    const string OverlaySource =
        """
        task OverlayTask
        {
            init I1;
            exit End;
            I1 --> End;
        }

        """;

    string _root = null!;

    [SetUp]
    public void SetUp() {
        _root = Path.Combine(Path.GetTempPath(), "overlay-syntax-provider-tests", Guid.NewGuid().ToString("N"));
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
    public void GetSyntax_UnchangedFile_ReturnsCachedInstance() {

        var path     = WriteNavFile("a.nav", AlphaSource);
        var provider = new OverlaySyntaxProvider();

        var first  = provider.GetSyntax(path);
        var second = provider.GetSyntax(path);

        Assert.That(first,             Is.Not.Null);
        Assert.That(second,            Is.SameAs(first));
        Assert.That(first!.ToString(), Does.Contain("Alpha"));
    }

    [Test]
    public void GetSyntax_OutOfBandContentChange_IsPickedUpWithoutInvalidate() {

        var path     = WriteNavFile("a.nav", AlphaSource);
        var provider = new OverlaySyntaxProvider();

        var before = provider.GetSyntax(path);

        // Änderung an der Platte vorbei am Provider — kein SetOverlay/InvalidateCache.
        WriteNavFile("a.nav", BetaSource);

        var after = provider.GetSyntax(path);

        Assert.That(after,             Is.Not.SameAs(before));
        Assert.That(after!.ToString(), Does.Contain("Beta"));
    }

    [Test]
    public void GetSyntax_TimestampOnlyChange_InvalidatesCache() {

        var path     = WriteNavFile("a.nav", AlphaSource);
        var provider = new OverlaySyntaxProvider();

        var before = provider.GetSyntax(path);

        // Inhalt und Länge bleiben identisch — nur der Zeitstempel wandert.
        File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(path).AddSeconds(2));

        var after = provider.GetSyntax(path);

        Assert.That(after, Is.Not.SameAs(before));
    }

    [Test]
    public void GetSyntax_DeletedAndRecreatedFile_IsPickedUp() {

        var path     = WriteNavFile("a.nav", AlphaSource);
        var provider = new OverlaySyntaxProvider();

        Assert.That(provider.GetSyntax(path), Is.Not.Null);

        File.Delete(path);
        Assert.That(provider.GetSyntax(path), Is.Null);
        // Wiederholter Zugriff auf die fehlende Datei bleibt Cache-Treffer (default-Stempel stabil).
        Assert.That(provider.GetSyntax(path), Is.Null);

        WriteNavFile("a.nav", BetaSource);
        var reborn = provider.GetSyntax(path);

        Assert.That(reborn,             Is.Not.Null);
        Assert.That(reborn!.ToString(), Does.Contain("Beta"));
    }

    [Test]
    public void GetSyntax_Overlay_IsAuthoritative_NoDiskPolling() {

        var path       = WriteNavFile("a.nav", AlphaSource);
        var normalized = PathHelper.NormalizePath(path);
        var provider   = new OverlaySyntaxProvider();

        provider.SetOverlay(normalized, OverlaySource);

        var overlayFirst = provider.GetSyntax(path);
        Assert.That(overlayFirst!.ToString(), Does.Contain("OverlayTask"));

        // Platten-Änderung darf das Overlay nicht verdrängen — kein Stempel-Poll für offene Dokumente.
        WriteNavFile("a.nav", BetaSource);

        var overlaySecond = provider.GetSyntax(path);
        Assert.That(overlaySecond, Is.SameAs(overlayFirst));

        provider.RemoveOverlay(normalized);

        var disk = provider.GetSyntax(path);
        Assert.That(disk!.ToString(), Does.Contain("Beta"));
    }

    [Test]
    public void GetSyntax_ConcurrentMisses_ShareSingleInstance() {

        // Regression gegen den Cache-Stampede: viele parallele Erst-Zugriffe auf dieselbe Datei treffen
        // zeitgleich auf einen leeren Cache. Ohne Single-Flight parst jeder Thread selbst und schreibt
        // last-writer-wins eine eigene Instanz — der aufsitzende Tier-2-Semantik-Cache (Validierung per
        // Referenzgleichheit) verfehlt seinen Treffer beim nächsten Scan dann grundlos. Mit Single-Flight
        // teilen sich alle Zugriffe genau EINE Instanz.
        var path = WriteNavFile("a.nav", AlphaSource);

        const int workers = 16;
        const int rounds  = 50;

        for (var round = 0; round < rounds; round++) {

            // Frischer Provider je Runde: der Cache ist leer, der Stampede also erzwungen.
            var provider = new OverlaySyntaxProvider();
            var results  = new CodeGenerationUnitSyntax[workers];
            var threads  = new Thread[workers];

            // Eigene Threads (kein Thread-Pool) + Startgatter: alle Worker starten wirklich zeitgleich.
            using var gate = new ManualResetEventSlim(initialState: false);

            for (var i = 0; i < workers; i++) {
                var index = i;
                threads[i] = new Thread(() => {
                    gate.Wait();
                    results[index] = provider.GetSyntax(path);
                });
                threads[i].Start();
            }

            gate.Set();
            foreach (var thread in threads) {
                thread.Join();
            }

            var reference = results[0];
            Assert.That(reference, Is.Not.Null,           $"Runde {round}");
            Assert.That(results,   Is.All.SameAs(reference), $"Runde {round}: Stampede lieferte mehrere Instanzen");
        }
    }

    string WriteNavFile(string name, string content) {
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, content);
        return path;
    }

}
