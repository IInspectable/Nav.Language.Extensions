#region Using Directives

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using NUnit.Framework;

#endregion

namespace Pharmatechnik.Nav.Language.BuildTasks.Tests;

/// <summary>
/// MSBuild-Integrationstests fuer den inkrementellen Nav-Build (<c>Pharmatechnik.Nav.Language.targets</c>).
/// Gebaut wird gegen das versandfertige Tool-Layout in <c>deploy\Build Tools</c> (echte, self-contained
/// <c>nav.exe</c>) via Full-Framework-MSBuild.exe. Fehlt das Layout oder MSBuild, werden die Tests
/// als <c>Ignored</c> markiert (Hinweis: zuerst `n publish`).
///
/// Primaere Signale sind dateibasiert und damit sprach-/verbositaetsunabhaengig:
///   * Run-Marker (obj\nav.ran.marker) existiert ⇒ der GenerateNavCode-Body lief (Regen/Erstlauf),
///   * Manifest (obj\nav.outputs.txt) Existenz + Zeitstempel,
///   * Existenz der generierten .cs.
/// </summary>
[TestFixture]
public class IncrementalBuildTests {

    [OneTimeTearDown]
    public void CleanupToolLayout() {
        BuildEnvironment.Cleanup();
    }

    static Dictionary<string, string> Incremental(params (string Key, string Value)[] extra) {
        var props = new Dictionary<string, string> { ["NavIncremental"] = "true" };
        foreach (var (key, value) in extra) {
            props[key] = value;
        }

        return props;
    }

    [Test]
    public void Erstbuild_generiert_Manifest_und_Outputs() {

        using var ws = NavBuildWorkspace.Create();
        ws.AddNavFile();
        ws.WriteProject();

        var result = ws.Build(Incremental());

        Assert.That(result.Succeeded, Is.True, $"Build fehlgeschlagen:\n{result.Output}\n{result.Error}");
        Assert.That(ws.BodyRan,       Is.True, "Run-Marker fehlt — der Codegen-Body lief beim Erstbuild nicht.");
        Assert.That(ws.ManifestExists, Is.True, "Manifest wurde beim Erstbuild nicht geschrieben.");

        var manifest = ws.ReadManifest();
        Assert.That(manifest, Is.Not.Empty, "Manifest ist leer — es wurden keine Outputs erzeugt.");
        Assert.That(manifest, Has.All.Exist, "Eine im Manifest gelistete Ausgabedatei existiert nicht.");
    }

    [Test]
    public void Zweiter_Build_ohne_Aenderung_wird_uebersprungen() {

        using var ws = NavBuildWorkspace.Create();
        ws.AddNavFile();
        ws.WriteProject();

        Assert.That(ws.Build(Incremental()).Succeeded, Is.True);
        var firstTimestamp = ws.ManifestTimestampUtc;

        var second = ws.Build(Incremental());

        Assert.That(second.Succeeded,           Is.True);
        Assert.That(ws.BodyRan,                 Is.False, "Body lief erneut, obwohl nichts geaendert wurde (kein Skip).");
        Assert.That(ws.ManifestTimestampUtc,    Is.EqualTo(firstTimestamp), "Manifest wurde beim Skip neu geschrieben.");
    }

    [Test]
    public void Geaenderte_Nav_Datei_loest_Regen_aus() {

        using var ws = NavBuildWorkspace.Create();
        var navFile = ws.AddNavFile();
        ws.WriteProject();

        Assert.That(ws.Build(Incremental()).Succeeded, Is.True);

        // .nav anfassen: mtime klar in die Zukunft => Input neuer als Manifest-Output => Regen.
        File.SetLastWriteTimeUtc(navFile, DateTime.UtcNow.AddMinutes(1));

        Assert.That(ws.Build(Incremental()).Succeeded, Is.True);
        Assert.That(ws.BodyRan, Is.True, "Geaenderte .nav-Datei loeste keinen Regen aus.");
    }

    [Test]
    public void Geloeschte_Ausgabedatei_loest_Regen_aus() {

        using var ws = NavBuildWorkspace.Create();
        ws.AddNavFile();
        ws.WriteProject();

        Assert.That(ws.Build(Incremental()).Succeeded, Is.True);

        var deleted = ws.ReadManifest().First();
        File.Delete(deleted);

        Assert.That(ws.Build(Incremental()).Succeeded, Is.True);
        Assert.That(ws.BodyRan,         Is.True,  "Fehlende Ausgabedatei loeste keinen Regen aus (Missing-Output-Erkennung).");
        Assert.That(File.Exists(deleted), Is.True, "Die geloeschte Ausgabedatei wurde nicht neu erzeugt.");
    }

    [Test]
    public void Aenderung_eines_content_relevanten_Parameters_loest_Regen_aus() {

        using var ws = NavBuildWorkspace.Create();
        ws.AddNavFile();
        ws.WriteProject();

        Assert.That(ws.Build(Incremental()).Succeeded, Is.True);

        // NavGenerateIwflClasses ist Teil des Param-Stamps (Iwfl=…) => Aenderung bumpt dessen mtime => Regen.
        var second = ws.Build(Incremental(("NavGenerateIwflClasses", "false")));

        Assert.That(second.Succeeded, Is.True);
        Assert.That(ws.BodyRan, Is.True, "Geaenderter content-relevanter Parameter loeste keinen Regen aus (Param-Stamp).");
    }

    [Test]
    public void Aenderung_eines_nicht_content_relevanten_Parameters_loest_keinen_Regen_aus() {

        using var ws = NavBuildWorkspace.Create();
        ws.AddNavFile();
        ws.WriteProject();

        Assert.That(ws.Build(Incremental()).Succeeded, Is.True);
        var firstTimestamp = ws.ManifestTimestampUtc;

        // NavFullPaths fliesst NICHT in den Param-Stamp ein => kein Regen.
        var second = ws.Build(Incremental(("NavFullPaths", "true")));

        Assert.That(second.Succeeded,        Is.True);
        Assert.That(ws.BodyRan,              Is.False, "Nicht content-relevanter Parameter loeste faelschlich einen Regen aus.");
        Assert.That(ws.ManifestTimestampUtc, Is.EqualTo(firstTimestamp));
    }

    [Test]
    public void NavForce_erzwingt_Vollregen_trotz_aktueller_Outputs() {

        using var ws = NavBuildWorkspace.Create();
        ws.AddNavFile();
        ws.WriteProject();

        Assert.That(ws.Build(Incremental()).Succeeded, Is.True);

        var second = ws.Build(Incremental(("NavForce", "true")));

        Assert.That(second.Succeeded, Is.True);
        Assert.That(ws.BodyRan, Is.True, "NavForce erzwang keinen Regen trotz aktueller Outputs.");
    }

    [Test]
    public void Nicht_inkrementell_laeuft_jeder_Build_und_schreibt_kein_Manifest() {

        using var ws = NavBuildWorkspace.Create();
        ws.AddNavFile();
        ws.WriteProject();

        // NavIncremental nicht gesetzt (Default false) => Always-Run, kein Manifest.
        Assert.That(ws.Build().Succeeded, Is.True);
        Assert.That(ws.ManifestExists, Is.False, "Im Non-Incremental-Modus darf kein Manifest entstehen.");

        var generated = Directory.EnumerateFiles(ws.WorkDir, "*.cs", SearchOption.AllDirectories).ToList();
        Assert.That(generated, Is.Not.Empty, "Es wurden keine .cs generiert.");

        // Always-Run-Nachweis: eine Ausgabedatei loeschen und erneut bauen — sie muss wiederkommen,
        // obwohl die uebrigen Outputs unveraendert/aktuell sind.
        File.Delete(generated[0]);

        Assert.That(ws.Build().Succeeded, Is.True);
        Assert.That(File.Exists(generated[0]), Is.True, "Non-Incremental-Build hat die geloeschte Datei nicht neu erzeugt.");
    }

    [Test]
    public void Keine_Nav_Dateien_baut_fehlerfrei() {

        using var ws = NavBuildWorkspace.Create();
        ws.WriteProject(); // keine GenerateNavCode-Items

        Assert.That(ws.Build().Succeeded, Is.True, "Build ohne .nav-Dateien schlug fehl.");
    }

    [Test]
    public void Geaenderte_taskref_Abhaengigkeit_loest_Regen_aus() {

        using var ws = NavBuildWorkspace.Create();
        ws.AddNavFile("RefTask.nav");                                 // GenerateNavCode-Item
        var dependency = ws.AddDependencyFile("RefDependency.nav");   // NICHT als Input gelistet
        ws.WriteProject();

        Assert.That(ws.Build(Incremental()).Succeeded, Is.True);

        // Die per taskref eingelesene Abhaengigkeit wurde als discovered input protokolliert.
        var deps = ws.ReadDependencyManifest();
        Assert.That(deps.Select(Path.GetFileName), Has.Member("RefDependency.nav"),
                    "Die taskref-Abhaengigkeit fehlt im Deps-Manifest (nav.inputs.txt).");

        // Baseline: zweiter Build ohne Aenderung wird uebersprungen.
        Assert.That(ws.Build(Incremental()).Succeeded, Is.True);
        Assert.That(ws.BodyRan, Is.False, "Body lief ohne Aenderung erneut (kein Skip).");

        // Abhaengigkeit anfassen: mtime klar in die Zukunft. Sie ist KEIN GenerateNavCode-Item —
        // ohne Deps-Tracking wuerde der Build sie nicht sehen und faelschlich ueberspringen.
        File.SetLastWriteTimeUtc(dependency, DateTime.UtcNow.AddMinutes(1));

        Assert.That(ws.Build(Incremental()).Succeeded, Is.True);
        Assert.That(ws.BodyRan, Is.True, "Geaenderte taskref-Abhaengigkeit loeste keinen Regen aus.");
    }

    [Test]
    public void Manifest_enthaelt_absolute_eindeutige_sortierte_Pfade() {

        using var ws = NavBuildWorkspace.Create();
        ws.AddNavFile();
        ws.WriteProject();

        Assert.That(ws.Build(Incremental()).Succeeded, Is.True);

        var manifest = ws.ReadManifest();
        Assert.That(manifest, Is.Not.Empty);

        Assert.That(manifest.All(Path.IsPathRooted), Is.True, "Manifest enthaelt nicht-absolute Pfade.");

        var distinct = manifest.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        Assert.That(distinct.Count, Is.EqualTo(manifest.Length), "Manifest enthaelt Duplikate.");

        var sorted = manifest.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
        Assert.That(manifest, Is.EqualTo(sorted), "Manifest ist nicht case-insensitiv sortiert.");
    }
}
