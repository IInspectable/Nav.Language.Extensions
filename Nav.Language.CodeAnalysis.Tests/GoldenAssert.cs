#region Using Directives

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

using NUnit.Framework;

using Location = Pharmatechnik.Nav.Language.Location;

#endregion

namespace Nav.Language.CodeAnalysis.Tests;

/// <summary>
/// Datei-basierte Golden-Snapshot-Prüfung für die Navigationstests — Muster wie
/// <c>SyntaxGoldenTests</c> (Nav.Language.Tests) und <c>SnapshotAssert</c> der SourceGenerator-Tests.
/// <para>
/// Die Erwartung liegt als <c>&lt;Szenario&gt;.expected</c> neben dem aufrufenden Test unter
/// <c>Snapshots\&lt;TestKlasse&gt;\</c> (Verzeichnis über <see cref="CallerFilePathAttribute"/> aufgelöst).
/// Bei Abweichung <i>oder</i> fehlender Golden-Datei wird das Ist-Ergebnis als <c>.actual</c> daneben
/// geschrieben (für den Diff) und der Test schlägt fehl.
/// </para>
/// <para>
/// Jede Golden-Datei trägt einen selbstdokumentierenden Kopf aus <c>//</c>-Zeilen: die
/// Navigationsrichtung (<see cref="NavigationDirection"/>, z.B. <c>Nav → C#</c>) plus die
/// Testbeschreibung. So ist beim Diff ohne Blick in den Testcode ersichtlich, was der Snapshot prüft.
/// </para>
/// <para>
/// <b>Regenerieren</b> (nur nach bewusster Prüfung der Sprungziele): den Testlauf mit gesetzter
/// Umgebungsvariable <c>NAV_UPDATE_GOLDEN=1</c> starten — dann schreibt jeder Aufruf seine
/// <c>.expected</c> neu (und räumt ein evtl. vorhandenes <c>.actual</c> weg), statt zu vergleichen.
/// </para>
/// </summary>
static class GoldenAssert {

    const string UpdateEnvironmentVariable = "NAV_UPDATE_GOLDEN";

    static readonly UTF8Encoding Utf8Bom = new(encoderShouldEmitUTF8Identifier: true);

    static bool UpdateRequested =>
        Environment.GetEnvironmentVariable(UpdateEnvironmentVariable) == "1";

    /// <summary>
    /// Bequem-Overload: serialisiert ein einzelnes Navigationsziel über <see cref="NavigationSnapshot"/>
    /// und vergleicht es gegen die Golden-Datei. Nimmt den Tests das manuelle
    /// <c>NavigationSnapshot.Serialize(…)</c>-Einwickeln ab; <see cref="CallerFilePathAttribute"/> wird
    /// durchgereicht, damit die Golden weiterhin im <c>Snapshots\&lt;Testklasse&gt;\</c> des <b>Testfiles</b> landet.
    /// </summary>
    public static void Match(Location location, CodeAnalysisTestContext context, string scenario,
                             NavigationDirection direction, string description,
                             [CallerFilePath] string callerFilePath = "") {
        Match(NavigationSnapshot.Serialize(location, context), scenario, direction, description, callerFilePath);
    }

    /// <summary>
    /// Bequem-Overload für eine Menge von Navigationszielen (z.B. alle Aufrufstellen oder die mehrdeutigen
    /// Exit-Punkte) — siehe die Einzel-Location-Überladung.
    /// </summary>
    public static void Match(IEnumerable<Location> locations, CodeAnalysisTestContext context, string scenario,
                             NavigationDirection direction, string description,
                             [CallerFilePath] string callerFilePath = "") {
        Match(NavigationSnapshot.Serialize(locations, context), scenario, direction, description, callerFilePath);
    }

    /// <summary>
    /// Vergleicht <paramref name="actual"/> gegen die Golden-Datei <paramref name="scenario"/>.expected.
    /// Der Datei wird ein Kopf aus <paramref name="direction"/> und <paramref name="description"/>
    /// vorangestellt (siehe Klassen-Doku).
    /// </summary>
    public static void Match(string actual, string scenario, NavigationDirection direction, string description,
                             [CallerFilePath] string callerFilePath = "") {

        var content = BuildContent(actual, direction, description);

        var directory = Path.Combine(
            Path.GetDirectoryName(callerFilePath)!,
            "Snapshots",
            Path.GetFileNameWithoutExtension(callerFilePath));

        Directory.CreateDirectory(directory);

        var expectedPath = Path.Combine(directory, scenario + ".expected");
        var actualPath   = Path.Combine(directory, scenario + ".actual");

        if (UpdateRequested) {
            File.WriteAllText(expectedPath, content, Utf8Bom);
            DeleteIfExists(actualPath);
            Assert.Pass($"Golden aktualisiert (NAV_UPDATE_GOLDEN=1): {expectedPath}");
            return;
        }

        if (!File.Exists(expectedPath)) {
            File.WriteAllText(actualPath, content, Utf8Bom);
            Assert.Fail($"Golden-Datei fehlt: {expectedPath}\n"                                     +
                        $"Ist-Ergebnis geschrieben nach: {actualPath}\n"                            +
                        $"Regenerieren (nach Prüfung): den Testlauf mit {UpdateEnvironmentVariable}=1 starten.");
            return;
        }

        var expected = Normalize(File.ReadAllText(expectedPath));

        if (Normalize(content) == expected) {
            DeleteIfExists(actualPath);
            return;
        }

        File.WriteAllText(actualPath, content, Utf8Bom);
        Assert.That(Normalize(content), Is.EqualTo(expected),
                    $"Golden-Abweichung gegenüber {expectedPath}.\nIst-Ergebnis: {actualPath}");
    }

    /// <summary>
    /// Stellt der Payload den <c>//</c>-Kopf voran: eine Zeile Richtungs-Pfeil, dann die
    /// (ggf. mehrzeilige) Beschreibung, eine Leerzeile, dann <paramref name="actual"/>.
    /// </summary>
    static string BuildContent(string actual, NavigationDirection direction, string description) {

        var sb = new StringBuilder();

        AppendCommentLine(sb, direction.ToArrowLabel());

        foreach (var line in description.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n')) {
            AppendCommentLine(sb, line.TrimEnd());
        }

        sb.Append('\n');
        sb.Append(actual);

        return sb.ToString();
    }

    static void AppendCommentLine(StringBuilder sb, string text) {
        sb.Append(text.Length == 0 ? "//" : "// " + text);
        sb.Append('\n');
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
