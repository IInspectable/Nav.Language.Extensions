#region Using Directives

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

using NUnit.Framework;

#endregion

namespace Nav.Language.CodeAnalysis.Tests;

/// <summary>
/// Datei-basierte Golden-Snapshot-Prüfung für die Navigationstests — Muster wie
/// <c>SyntaxGoldenTests</c> (Nav.Language.Tests) und <c>SnapshotAssert</c> der SourceGenerator-Tests.
/// <para>
/// Die Erwartung liegt als <c>&lt;Szenario&gt;.approved</c> neben dem aufrufenden Test unter
/// <c>Snapshots\&lt;TestKlasse&gt;\</c> (Verzeichnis über <see cref="CallerFilePathAttribute"/> aufgelöst).
/// Bei Abweichung <i>oder</i> fehlender Golden-Datei wird das Ist-Ergebnis als <c>.received</c> daneben
/// geschrieben (für den Diff) und der Test schlägt fehl.
/// </para>
/// <para>
/// <b>Regenerieren</b> (nur nach bewusster Prüfung der Sprungziele): den Testlauf mit gesetzter
/// Umgebungsvariable <c>NAV_UPDATE_GOLDEN=1</c> starten — dann schreibt jeder Aufruf seine
/// <c>.approved</c> neu (und räumt ein evtl. vorhandenes <c>.received</c> weg), statt zu vergleichen.
/// </para>
/// </summary>
static class GoldenAssert {

    const string UpdateEnvironmentVariable = "NAV_UPDATE_GOLDEN";

    static readonly UTF8Encoding Utf8Bom = new(encoderShouldEmitUTF8Identifier: true);

    static bool UpdateRequested =>
        Environment.GetEnvironmentVariable(UpdateEnvironmentVariable) == "1";

    /// <summary>
    /// Vergleicht <paramref name="actual"/> gegen die Golden-Datei <paramref name="scenario"/>.approved.
    /// </summary>
    public static void Match(string actual, string scenario, [CallerFilePath] string callerFilePath = "") {

        var directory = Path.Combine(
            Path.GetDirectoryName(callerFilePath)!,
            "Snapshots",
            Path.GetFileNameWithoutExtension(callerFilePath));

        Directory.CreateDirectory(directory);

        var approvedPath = Path.Combine(directory, scenario + ".approved");
        var receivedPath = Path.Combine(directory, scenario + ".received");

        if (UpdateRequested) {
            File.WriteAllText(approvedPath, actual, Utf8Bom);
            DeleteIfExists(receivedPath);
            Assert.Pass($"Golden aktualisiert (NAV_UPDATE_GOLDEN=1): {approvedPath}");
            return;
        }

        if (!File.Exists(approvedPath)) {
            File.WriteAllText(receivedPath, actual, Utf8Bom);
            Assert.Fail($"Golden-Datei fehlt: {approvedPath}\n"                                     +
                        $"Ist-Ergebnis geschrieben nach: {receivedPath}\n"                          +
                        $"Regenerieren (nach Prüfung): den Testlauf mit {UpdateEnvironmentVariable}=1 starten.");
            return;
        }

        var expected = Normalize(File.ReadAllText(approvedPath));

        if (Normalize(actual) == expected) {
            DeleteIfExists(receivedPath);
            return;
        }

        File.WriteAllText(receivedPath, actual, Utf8Bom);
        Assert.That(Normalize(actual), Is.EqualTo(expected),
                    $"Golden-Abweichung gegenüber {approvedPath}.\nIst-Ergebnis: {receivedPath}");
    }

    static void DeleteIfExists(string path) {
        if (File.Exists(path)) {
            File.Delete(path);
        }
    }

    static string Normalize(string value) {
        return value.Replace("\r\n", "\n").Replace("\r", "\n").Trim();
    }

}
