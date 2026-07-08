#region Using Directives

using System;
using System.Diagnostics;
using System.IO;
using System.Text;

using NUnit.Framework;

#endregion

namespace Pharmatechnik.Nav.Language.BuildTasks.Tests;

/// <summary>
/// Stellt das Tool-Layout fuer die Integrationstests selbst bereit und loest die Full-Framework-
/// <c>MSBuild.exe</c> auf — ohne Abhaengigkeit von <c>n publish</c>/<c>deploy\Build Tools</c>.
///
/// Das Layout (Targets + Task-DLL + <c>nav.exe</c> co-lokalisiert, wie es
/// <c>_NavToolPath = $(MSBuildThisFileDirectory)nav.exe</c> und der <c>UsingTask</c> erwarten) wird in
/// einem Temp-Verzeichnis aus den NORMALEN Build-Outputs zusammengestellt:
///   * framework-abhaengige <c>nav.exe</c> (+ Runtime-Configs + Engine-DLLs) aus <c>Nav.Cli\bin\&lt;cfg&gt;</c>,
///   * Targets + Task-DLL aus <c>Nav.Language.BuildTasks\bin\&lt;cfg&gt;</c>.
/// Damit testen die Faelle immer die AKTUELLEN Sourcen (nicht ein evtl. veraltetes Publish), und es
/// genuegt, dass die Solution gebaut wurde (im CI durch den Default-Build, lokal durch `n build`).
///
/// Fehlt eine Voraussetzung (Build-Outputs oder VS/MSBuild), werden die Tests mit klarem Hinweis als
/// <c>Ignored</c> markiert — nie rot.
/// </summary>
static class BuildEnvironment {

    const string SlnxName        = "Nav.Language.Extensions.slnx";
    const string TargetsFileName = "Pharmatechnik.Nav.Language.targets";
    const string TaskDllName     = "Pharmatechnik.Nav.Language.BuildTasks.dll";
    const string NavToolName     = "nav.exe";

    static readonly object Gate = new();

    static string _toolDir;
    static string _msBuildExe;

    /// <summary>
    /// Temp-Verzeichnis mit dem zusammengestellten Tool-Layout (einmal pro Testlauf, danach gecached).
    /// Fehlen die Build-Outputs, wird der Test uebersprungen.
    /// </summary>
    public static string ToolDirectoryOrIgnore() {

        lock (Gate) {

            if (_toolDir != null) {
                return _toolDir;
            }

            var repoRoot = RepoRootOrIgnore();
            var config   = CurrentConfiguration();

            var cliBin   = Path.Combine(repoRoot, "Nav.Cli", "bin", config);
            var tasksBin = Path.Combine(repoRoot, "Nav.Language.BuildTasks", "bin", config);

            var navExe  = Path.Combine(cliBin,   NavToolName);
            var targets = Path.Combine(tasksBin, TargetsFileName);
            var taskDll = Path.Combine(tasksBin, TaskDllName);

            if (!File.Exists(navExe) || !File.Exists(targets) || !File.Exists(taskDll)) {
                Assert.Ignore($"Build-Outputs nicht gefunden (Konfiguration '{config}'). Bitte zuerst die Solution bauen " +
                              "(`n build` bzw. `dotnet build`). Erwartet:\n" +
                              $"  {navExe}\n  {targets}\n  {taskDll}");
            }

            var toolDir = Path.Combine(Path.GetTempPath(), "nav-incbuild-tool", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(toolDir);

            // Komplette (top-level) CLI-Ausgabe kopieren: framework-abhaengige nav.exe ist nur lauffaehig
            // zusammen mit nav.dll + *.runtimeconfig.json + *.deps.json + Engine-DLLs.
            foreach (var file in Directory.EnumerateFiles(cliBin)) {
                File.Copy(file, Path.Combine(toolDir, Path.GetFileName(file)), overwrite: true);
            }

            File.Copy(targets, Path.Combine(toolDir, TargetsFileName), overwrite: true);
            File.Copy(taskDll, Path.Combine(toolDir, TaskDllName),     overwrite: true);

            return _toolDir = toolDir;
        }
    }

    public static string TargetsFileOrIgnore() {
        return Path.Combine(ToolDirectoryOrIgnore(), TargetsFileName);
    }

    /// <summary>Raeumt das zusammengestellte Tool-Verzeichnis auf (aus dem OneTimeTearDown der Fixture).</summary>
    public static void Cleanup() {
        lock (Gate) {
            if (_toolDir != null) {
                try {
                    Directory.Delete(_toolDir, recursive: true);
                } catch {
                    // best-effort
                }

                _toolDir = null;
            }
        }
    }

    /// <summary>Pfad zur Full-Framework-MSBuild.exe (vswhere). Fehlt VS/MSBuild, wird uebersprungen.</summary>
    public static string MsBuildExeOrIgnore() {

        lock (Gate) {

            if (_msBuildExe != null) {
                return _msBuildExe;
            }

            var programFilesX86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)")
                               ?? @"C:\Program Files (x86)";

            var vswhere = Path.Combine(programFilesX86, "Microsoft Visual Studio", "Installer", "vswhere.exe");
            if (!File.Exists(vswhere)) {
                Assert.Ignore("vswhere.exe nicht gefunden — ist Visual Studio installiert?");
            }

            var output = RunAndCaptureStdout(vswhere,
                                             "-latest",
                                             "-requires", "Microsoft.Component.MSBuild",
                                             "-find", @"MSBuild\**\Bin\MSBuild.exe");

            var msbuild = FirstNonEmptyLine(output);
            if (msbuild == null || !File.Exists(msbuild)) {
                Assert.Ignore("MSBuild.exe konnte ueber vswhere nicht gefunden werden.");
            }

            return _msBuildExe = msbuild;
        }
    }

    static string RepoRootOrIgnore() {

        var dir = TestContext.CurrentContext.TestDirectory;

        while (dir != null) {
            if (File.Exists(Path.Combine(dir, SlnxName))) {
                return dir;
            }

            dir = Path.GetDirectoryName(dir);
        }

        Assert.Ignore($"Repo-Root ('{SlnxName}') nicht gefunden.");
        return null; // unerreichbar
    }

    static string CurrentConfiguration() {
        // TestDirectory = ...\bin\<config>\net10.0 => <config> ist das Verzeichnis ueber dem TFM-Ordner.
        var tfmDir    = TestContext.CurrentContext.TestDirectory.TrimEnd(Path.DirectorySeparatorChar);
        var configDir = Path.GetDirectoryName(tfmDir);
        var config    = configDir != null ? Path.GetFileName(configDir) : null;

        return string.IsNullOrEmpty(config) ? "Debug" : config;
    }

    static string FirstNonEmptyLine(string text) {
        foreach (var line in text.Split('\n')) {
            var trimmed = line.Trim();
            if (trimmed.Length > 0) {
                return trimmed;
            }
        }

        return null;
    }

    static string RunAndCaptureStdout(string exe, params string[] args) {

        var psi = new ProcessStartInfo(exe) {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
            StandardOutputEncoding = Encoding.UTF8,
        };

        foreach (var arg in args) {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi);
        var stdout = process!.StandardOutput.ReadToEnd();
        process.WaitForExit();

        return stdout;
    }
}
