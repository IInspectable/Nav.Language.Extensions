#region Using Directives

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Utilities.IO;

#endregion

namespace Nav.Language.Tests.Workspace;

/// <summary>
/// Tier-2-Semantik-Cache auf Scan-Ebene: der komplette Host-Pfad (<see cref="NavWorkspaceCore"/> →
/// <see cref="NavSolution.ProcessCodeGenerationUnitsAsync"/>) liefert bei Wiederhol-Scans referenzgleiche
/// Units; out-of-band geänderte Dateien (Platte, ohne jeden Invalidate-Aufruf) und Overlay-Edits drehen
/// über Tier 1 die Syntax-Instanz und bauen genau die betroffenen Units neu — Nachbarn bleiben Treffer.
/// </summary>
[TestFixture]
public class NavWorkspaceCoreSemanticCacheTests {

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

    string _root = null!;

    [SetUp]
    public void SetUp() {
        _root = Path.Combine(Path.GetTempPath(), "workspace-semantic-cache-tests", Guid.NewGuid().ToString("N"));
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
    public async Task SecondScan_ReturnsSameUnitInstances() {

        WriteNavFile("lib.nav", LibSource);
        WriteNavFile("a.nav",   Consumer("A", "lib.nav", "Sub"));

        var workspace = await LoadWorkspaceAsync();

        var scan1 = await ScanAsync(workspace);
        var scan2 = await ScanAsync(workspace);

        Assert.That(scan1, Has.Count.EqualTo(2));
        Assert.That(scan2["a.nav"],   Is.SameAs(scan1["a.nav"]));
        Assert.That(scan2["lib.nav"], Is.SameAs(scan1["lib.nav"]));
    }

    [Test]
    public async Task OutOfBandChange_SecondScanRebuildsOnlyThatFile() {

        WriteNavFile("lib.nav", LibSource);
        WriteNavFile("a.nav",   Consumer("A", "lib.nav", "Sub"));
        WriteNavFile("b.nav",   Consumer("B", "lib.nav", "Sub"));

        var workspace = await LoadWorkspaceAsync();
        var scan1     = await ScanAsync(workspace);

        // Änderung an der Platte vorbei am Workspace — kein InvalidateCache, kein Overlay.
        WriteNavFile("a.nav", Consumer("ARenamed", "lib.nav", "Sub"));

        var scan2 = await ScanAsync(workspace);

        Assert.That(scan2["a.nav"],                                      Is.Not.SameAs(scan1["a.nav"]));
        Assert.That(scan2["a.nav"].TaskDefinitions.Select(t => t.Name),  Has.Member("ARenamed"));
        // Die Nachbarn hängen nicht an a.nav und bleiben Treffer.
        Assert.That(scan2["b.nav"],   Is.SameAs(scan1["b.nav"]));
        Assert.That(scan2["lib.nav"], Is.SameAs(scan1["lib.nav"]));
    }

    [Test]
    public async Task OutOfBandChange_OfInclude_RebuildsConsumer() {

        WriteNavFile("lib.nav", LibSource);
        WriteNavFile("a.nav",   Consumer("A", "lib.nav", "Sub"));

        var workspace = await LoadWorkspaceAsync();
        var scan1     = await ScanAsync(workspace);

        // Nur das Include ändert sich — a.nav bleibt unangetastet.
        WriteNavFile("lib.nav", LibWithSub2Source);

        var scan2 = await ScanAsync(workspace);

        Assert.That(scan2["a.nav"], Is.Not.SameAs(scan1["a.nav"]));
        Assert.That(scan2["a.nav"].Includes.Single().TaskDeclarations.Select(d => d.Name), Has.Member("Sub2"));
    }

    [Test]
    public async Task OutOfBandChange_OfTransitiveInclude_KeepsIndirectConsumerCached() {

        // Kette: a inkludiert b, b inkludiert c — Includes wirken nicht transitiv, a hängt NICHT an c.
        WriteNavFile("c.nav", LibSource);
        WriteNavFile("b.nav", Consumer("B", "c.nav", "Sub"));
        WriteNavFile("a.nav", Consumer("A", "b.nav", "B"));

        var workspace = await LoadWorkspaceAsync();
        var scan1     = await ScanAsync(workspace);

        WriteNavFile("c.nav", LibWithSub2Source);

        var scan2 = await ScanAsync(workspace);

        // a bleibt Treffer (c ist kein direktes Include von a) …
        Assert.That(scan2["a.nav"], Is.SameAs(scan1["a.nav"]));
        // … die direkten Konsumenten von c werden neu gebaut.
        Assert.That(scan2["b.nav"], Is.Not.SameAs(scan1["b.nav"]));
        Assert.That(scan2["c.nav"], Is.Not.SameAs(scan1["c.nav"]));
    }

    [Test]
    public async Task OverlayEdit_InvalidatesUnit_CloseRestoresDiskTruth() {

        WriteNavFile("lib.nav", LibSource);
        WriteNavFile("a.nav",   Consumer("A", "lib.nav", "Sub"));

        var workspace = await LoadWorkspaceAsync();
        var scan1     = await ScanAsync(workspace);

        // Ungespeicherter Editor-Puffer: das Overlay ist autoritativ vor der Platte.
        var overlayPath = PathHelper.NormalizePath(Path.Combine(_root, "a.nav"))!;
        workspace.OpenOrUpdate(overlayPath, Consumer("AOverlay", "lib.nav", "Sub"));

        var scan2 = await ScanAsync(workspace);

        Assert.That(scan2["a.nav"],                                     Is.Not.SameAs(scan1["a.nav"]));
        Assert.That(scan2["a.nav"].TaskDefinitions.Select(t => t.Name), Has.Member("AOverlay"));
        Assert.That(scan2["lib.nav"],                                   Is.SameAs(scan1["lib.nav"]));

        // Schließen: die Wahrheit liegt wieder auf Platte — der Scan spiegelt den Disk-Stand.
        workspace.Close(overlayPath);

        var scan3 = await ScanAsync(workspace);

        Assert.That(scan3["a.nav"],                                     Is.Not.SameAs(scan2["a.nav"]));
        Assert.That(scan3["a.nav"].TaskDefinitions.Select(t => t.Name), Has.Member("A"));
    }

    // --- Infrastruktur ---------------------------------------------------------------------------------

    string WriteNavFile(string fileName, string content) {
        var path = Path.Combine(_root, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    async Task<NavWorkspaceCore> LoadWorkspaceAsync() {
        var workspace = new NavWorkspaceCore();
        await workspace.LoadAsync(_root, CancellationToken.None);
        return workspace;
    }

    /// <summary>
    /// Solution-weiter Scan über den Host-Pfad; Ergebnis keyed auf den Dateinamen (case-insensitiv,
    /// weil Scan-Pfade und normalisierte Overlay-Pfade in der Schreibweise abweichen können).
    /// </summary>
    static async Task<Dictionary<string, CodeGenerationUnit>> ScanAsync(NavWorkspaceCore workspace) {

        var units = new Dictionary<string, CodeGenerationUnit>(StringComparer.OrdinalIgnoreCase);

        await workspace.Solution.ProcessCodeGenerationUnitsAsync(
            unit => {
                var fileName = Path.GetFileName(unit.Syntax.SyntaxTree.SourceText.FileInfo!.FullName);
                units[fileName] = unit;
                return Task.CompletedTask;
            },
            startingUnit: null,
            CancellationToken.None);

        return units;
    }

}
