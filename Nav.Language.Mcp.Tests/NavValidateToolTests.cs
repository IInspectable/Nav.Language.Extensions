#region Using Directives

using System.IO;
using System.Linq;

using NUnit.Framework;

using Nav.Language.Mcp.Tests.Infrastructure;

using Pharmatechnik.Nav.Language.Mcp.Tools;

#endregion

namespace Nav.Language.Mcp.Tests;

/// <summary>
/// Tests des MCP-Tools <c>nav_validate</c> (public statische Tool-Methode, direkt aufgerufen). Verifiziert
/// die Diagnostics einer Einzeldatei (Counts/Ok je Schweregrad), die <b>Fresh-Read-Semantik</b> — der
/// „Agent editiert → fragt ab"-Kernfluss: eine auf Platte geänderte Datei ist bei der nächsten Abfrage
/// sofort sichtbar (Cache-Invalidierung pro Datei) — sowie den fehlertoleranten Pfad bei fehlender Datei.
/// </summary>
[TestFixture]
public class NavValidateToolTests {

    const string CleanTask =
        """
        task A
        {
            init i;
            exit e;
            i --> e;
        }
        """;

    // Genau ein Error (Nav0010): 'C' verweist auf eine nicht auflösbare Task und wird per 'I1 --> C' benutzt.
    const string BrokenTask =
        """
        task A
        {
            init I1;
            exit e1;
            task C;

            I1 --> e1;
            I1 --> C;
        }
        """;

    [Test]
    public void Validate_CleanFile_IsOkWithoutErrors() {

        using var ws = new McpTestWorkspace();
        var path = ws.WriteFile("clean.nav", CleanTask);

        var result = NavValidateTool.Validate(ws.Workspace, path);

        Assert.IsTrue(result.Ok);
        Assert.AreEqual(0, result.ErrorCount);
        Assert.IsNull(result.Error, "Bei einer gefundenen, parsebaren Datei bleibt das Error-Feld leer.");
    }

    [Test]
    public void Validate_BrokenFile_ReportsError() {

        using var ws = new McpTestWorkspace();
        var path = ws.WriteFile("broken.nav", BrokenTask);

        var result = NavValidateTool.Validate(ws.Workspace, path);

        Assert.IsFalse(result.Ok);
        Assert.AreEqual(1, result.ErrorCount);
        Assert.IsTrue(result.Diagnostics.Any(d => d.Severity == "Error"));
    }

    [Test]
    public void Validate_AfterEditOnDisk_SeesFreshState() {

        using var ws = new McpTestWorkspace();

        // Zustand 1: sauber -> Ok. Danach dieselbe Datei auf Platte kaputt schreiben und erneut abfragen:
        // der neue Stand muss sofort sichtbar sein (Fresh-Read pro Datei, keine Overlays).
        var path = ws.WriteFile("edited.nav", CleanTask);
        var before = NavValidateTool.Validate(ws.Workspace, path);
        Assert.IsTrue(before.Ok);
        Assert.AreEqual(0, before.ErrorCount);

        ws.WriteFile("edited.nav", BrokenTask);
        var afterBreak = NavValidateTool.Validate(ws.Workspace, path);
        Assert.IsFalse(afterBreak.Ok, "Die auf Platte eingebaute Fehlerlage ist sofort sichtbar.");
        Assert.AreEqual(1, afterBreak.ErrorCount);

        // Und wieder zurück: den Fehler entfernen -> die nächste Abfrage ist wieder sauber.
        ws.WriteFile("edited.nav", CleanTask);
        var afterFix = NavValidateTool.Validate(ws.Workspace, path);
        Assert.IsTrue(afterFix.Ok, "Der behobene Stand ist ebenso sofort sichtbar.");
        Assert.AreEqual(0, afterFix.ErrorCount);
    }

    [Test]
    public void Validate_MissingFile_ReturnsNotFoundResult() {

        using var ws = new McpTestWorkspace();

        // Eine nie geschriebene Datei: kein Wurf, sondern ein NotFound-Result mit gesetztem Error-Feld.
        var missing = Path.Combine(ws.Root, "does-not-exist.nav");

        var result = NavValidateTool.Validate(ws.Workspace, missing);

        Assert.IsFalse(result.Ok);
        Assert.IsNotNull(result.Error);
        CollectionAssert.IsEmpty(result.Diagnostics);
    }

}
