using System.IO;

using NUnit.Framework;

namespace Nav.Language.Tests;

/// <summary>
/// Loest Pfade zu Testdaten-Ordnern (die im Projektverzeichnis liegen und nicht ins Output kopiert
/// werden) tiefenunabhaengig auf. Frueher wurde fix von <c>bin\Debug</c> ausgegangen (<c>..\..</c>);
/// beim Multi-Targeting liegt das Output aber unter <c>bin\Debug\&lt;tfm&gt;</c>, also eine Ebene tiefer.
/// Statt einer festen Anzahl ".."-Schritte wird vom TestDirectory aus aufwaerts gesucht, bis der
/// gewuenschte relative Ordner existiert.
/// </summary>
static class TestDataDirectory {

    public static string Resolve(string relativePath) {

        var dir = TestContext.CurrentContext.TestDirectory;

        while (dir != null && !Directory.Exists(Path.Combine(dir, relativePath))) {
            dir = Path.GetDirectoryName(dir);
        }

        // Fallback: bisheriges Verhalten beibehalten, falls nichts gefunden wurde.
        dir ??= Path.Combine(TestContext.CurrentContext.TestDirectory, @"..\..");

        return Path.GetFullPath(Path.Combine(dir, relativePath));
    }
}
