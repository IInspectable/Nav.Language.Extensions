using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

using NUnit.Framework;

namespace Pharmatechnik.Nav.Language.Grammar.SourceGenerator.Tests.Shared;

/// <summary>
/// Datei-basierte Snapshot-Prüfung: vergleicht erzeugten Quelltext gegen eine eingecheckte
/// <c>Snapshots\Expected\&lt;TestKlasse&gt;\&lt;Name&gt;.cs</c> und schreibt das Ist-Ergebnis nach
/// <c>Snapshots\Actual\…</c> (gitignored). Fehlt die Expected-Datei, schlägt der Test fehl und nennt
/// den Pfad der Actual-Datei zum Übernehmen.
/// </summary>
public static class SnapshotAssert {

    public static void AssertSnapshot(string actualCode, string snapshotName, [CallerFilePath] string callerFilePath = "") {

        var projectDir = Path.GetDirectoryName(callerFilePath)!;
        var className  = Path.GetFileNameWithoutExtension(callerFilePath);

        var expectedPath = Path.Combine(projectDir, "Snapshots", "Expected", className, snapshotName + ".cs");
        var actualPath   = Path.Combine(projectDir, "Snapshots", "Actual",   className, snapshotName + ".cs");

        Directory.CreateDirectory(Path.GetDirectoryName(actualPath)!);
        File.WriteAllText(actualPath, actualCode, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        if (!File.Exists(expectedPath)) {
            Assert.Fail($"Erwartete Snapshot-Datei nicht gefunden: {expectedPath}\n" +
                        $"Ist-Ergebnis wurde geschrieben nach: {actualPath}\n" +
                        $"Tipp: Actual prüfen und nach Expected kopieren.");
        }

        var expected = Normalize(File.ReadAllText(expectedPath));
        var actual   = Normalize(actualCode);

        Assert.That(actual, Is.EqualTo(expected),
                    $"Snapshot-Abweichung gegenüber {expectedPath}. Ist-Ergebnis: {actualPath}");
    }

    static string Normalize(string value) {
        return value.Replace("\r\n", "\n").Replace("\r", "\n").Trim();
    }

}
