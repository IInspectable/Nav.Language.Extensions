#region Using Directives

using System;
using System.Diagnostics;
using System.IO;
using System.Text;

using NUnit.Framework;

#endregion

namespace Pharmatechnik.Nav.Language.BuildTasks.Tests;

/// <summary>
/// Loest die externen Voraussetzungen der Integrationstests auf: das versandfertige Tool-Layout
/// (<c>deploy\Build Tools</c> mit Targets + Task-DLL + self-contained <c>nav.exe</c>) und die
/// Full-Framework-<c>MSBuild.exe</c> (via vswhere, analog <c>Tools\Commands\Functions\Resolve-MsBuild.ps1</c>).
/// Fehlt eine Voraussetzung, werden die Tests mit klarem Hinweis als <c>Ignored</c> markiert — nie rot.
/// </summary>
static class BuildEnvironment {

    const string TargetsFileName = "Pharmatechnik.Nav.Language.targets";
    const string NavToolName     = "nav.exe";

    static string _cachedBuildToolsDir;
    static string _cachedMsBuildExe;

    /// <summary>
    /// Verzeichnis <c>deploy\Build Tools</c>. Fehlt es (oder die <c>nav.exe</c> darin), wird der Test
    /// uebersprungen.
    /// </summary>
    public static string BuildToolsDirectoryOrIgnore() {

        if (_cachedBuildToolsDir != null) {
            return _cachedBuildToolsDir;
        }

        var dir = TestContext.CurrentContext.TestDirectory;

        while (dir != null) {

            var candidate = Path.Combine(dir, "deploy", "Build Tools");
            if (File.Exists(Path.Combine(candidate, TargetsFileName))) {

                if (!File.Exists(Path.Combine(candidate, NavToolName))) {
                    Assert.Ignore($"'{Path.Combine(candidate, NavToolName)}' fehlt — bitte zuerst `n publish` ausfuehren " +
                                  "(ergaenzt die self-contained nav.exe in deploy\\Build Tools).");
                }

                return _cachedBuildToolsDir = candidate;
            }

            dir = Path.GetDirectoryName(dir);
        }

        Assert.Ignore("'deploy\\Build Tools' nicht gefunden — bitte zuerst `n publish` ausfuehren.");
        return null; // unerreichbar (Assert.Ignore wirft)
    }

    public static string TargetsFileOrIgnore() {
        return Path.Combine(BuildToolsDirectoryOrIgnore(), TargetsFileName);
    }

    /// <summary>Pfad zur Full-Framework-MSBuild.exe (vswhere). Fehlt VS/MSBuild, wird uebersprungen.</summary>
    public static string MsBuildExeOrIgnore() {

        if (_cachedMsBuildExe != null) {
            return _cachedMsBuildExe;
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

        return _cachedMsBuildExe = msbuild;
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
