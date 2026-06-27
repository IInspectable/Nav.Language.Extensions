#region Using Directives

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

using NUnit.Framework;

#endregion

namespace Pharmatechnik.Nav.Language.BuildTasks.Tests;

/// <summary>
/// Ein frisches Temp-Arbeitsverzeichnis mit einem minimalen MSBuild-Projekt, das die deploy-Targets
/// importiert. Kapselt das Schreiben des Fixture-Projekts, das Starten von MSBuild.exe (mit erzwungen
/// englischer Ausgabe) und die dateibasierten, sprachunabhaengigen Inkrementalitaets-Signale
/// (Manifest, Run-Marker, generierte .cs).
/// </summary>
sealed class NavBuildWorkspace: IDisposable {

    readonly string _msBuildExe;
    readonly string _targetsFile;
    readonly List<string> _navFiles = new();

    public string WorkDir      { get; }
    public string ProjectFile  => Path.Combine(WorkDir, "Fixture.proj");
    public string ObjDir       => Path.Combine(WorkDir, "obj");
    public string ManifestFile => Path.Combine(ObjDir, "nav.outputs.txt");
    public string RanMarker    => Path.Combine(ObjDir, "nav.ran.marker");

    NavBuildWorkspace(string workDir, string msBuildExe, string targetsFile) {
        WorkDir      = workDir;
        _msBuildExe  = msBuildExe;
        _targetsFile = targetsFile;
    }

    /// <summary>Legt das Arbeitsverzeichnis an (ueberspringt den Test, wenn Tool-Layout/MSBuild fehlen).</summary>
    public static NavBuildWorkspace Create() {

        var targets  = BuildEnvironment.TargetsFileOrIgnore();
        var msbuild  = BuildEnvironment.MsBuildExeOrIgnore();

        var workDir = Path.Combine(Path.GetTempPath(), "nav-incbuild-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);

        return new NavBuildWorkspace(workDir, msbuild, targets);
    }

    /// <summary>Kopiert eine .nav-Fixture aus dem TestData-Output ins Arbeitsverzeichnis.</summary>
    public string AddNavFile(string fixtureName = "SampleTask.nav", string targetName = null) {

        targetName ??= fixtureName;

        var source = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", fixtureName);
        var dest   = Path.Combine(WorkDir, targetName);

        File.Copy(source, dest, overwrite: true);
        _navFiles.Add(dest);

        return dest;
    }

    /// <summary>Schreibt (bzw. ueberschreibt) das Fixture-Projekt mit den aktuell registrierten .nav-Dateien.</summary>
    public void WriteProject() {

        var items = new StringBuilder();
        foreach (var navFile in _navFiles) {
            items.Append("    <GenerateNavCode Include=\"").Append(navFile).Append("\" />").Append('\n');
        }

        // Plain MSBuild (kein SDK): nur Properties/Items + Import der deploy-Targets. IntermediateOutputPath
        // muss vor der Target-Ausfuehrung gesetzt sein (die Targets leiten daraus Manifest/Marker ab).
        var proj = $@"<Project>
  <PropertyGroup>
    <IntermediateOutputPath>obj\</IntermediateOutputPath>
  </PropertyGroup>
  <ItemGroup>
{items}  </ItemGroup>
  <Import Project=""{_targetsFile}"" />
</Project>";

        File.WriteAllText(ProjectFile, proj, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    /// <summary>
    /// Startet MSBuild.exe auf das Fixture-Projekt (Target <c>GenerateNavCode</c>). Zusaetzliche
    /// MSBuild-Properties (z.B. NavIncremental, NavStrict) werden als <c>/p:Key=Value</c> uebergeben.
    /// </summary>
    public BuildResult Build(IDictionary<string, string> properties = null) {

        var psi = new ProcessStartInfo(_msBuildExe) {
            WorkingDirectory       = WorkDir,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding  = Encoding.UTF8,
        };

        // Deterministisch englische MSBuild-Ausgabe — haelt Fehlermeldungen in Test-Reports stabil,
        // unabhaengig von der installierten VS-Sprache. (Die Inkrementalitaets-Asserts selbst stuetzen
        // sich auf dateibasierte Signale, nicht auf Konsolentext.)
        psi.Environment["VSLANG"] = "1033";

        psi.ArgumentList.Add(ProjectFile);
        psi.ArgumentList.Add("/t:GenerateNavCode");
        psi.ArgumentList.Add("/v:normal");
        psi.ArgumentList.Add("/nologo");
        psi.ArgumentList.Add("/nr:false"); // kein Node-Reuse => keine haengenden MSBuild-Knoten sperren das Temp-Verzeichnis

        if (properties != null) {
            foreach (var kvp in properties) {
                psi.ArgumentList.Add($"/p:{kvp.Key}={kvp.Value}");
            }
        }

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        using var process = new Process { StartInfo = psi };
        process.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.Append(e.Data).Append('\n'); };
        process.ErrorDataReceived  += (_, e) => { if (e.Data != null) stderr.Append(e.Data).Append('\n'); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        return new BuildResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    /// <summary>Marker existiert ⇒ der GenerateNavCode-Body lief (inkrementeller Regen/Erstlauf).</summary>
    public bool BodyRan => File.Exists(RanMarker);

    public bool ManifestExists => File.Exists(ManifestFile);

    public string[] ReadManifest() =>
        File.Exists(ManifestFile) ? File.ReadAllLines(ManifestFile) : Array.Empty<string>();

    public DateTime ManifestTimestampUtc => File.GetLastWriteTimeUtc(ManifestFile);

    public void Dispose() {
        try {
            if (Directory.Exists(WorkDir)) {
                Directory.Delete(WorkDir, recursive: true);
            }
        } catch {
            // Aufraeumen ist best-effort; ein gesperrtes Temp-Verzeichnis soll den Testlauf nicht roetlich faerben.
        }
    }
}

/// <summary>Ergebnis eines MSBuild-Laufs inkl. (englischer) Skip-Erkennung fuer das Nav-Target.</summary>
sealed class BuildResult {

    public int    ExitCode { get; }
    public string Output   { get; }
    public string Error    { get; }

    public BuildResult(int exitCode, string output, string error) {
        ExitCode = exitCode;
        Output   = output;
        Error    = error;
    }

    public bool Succeeded => ExitCode == 0;
}
