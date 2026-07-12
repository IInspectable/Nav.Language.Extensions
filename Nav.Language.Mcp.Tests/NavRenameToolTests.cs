#region Using Directives

using System.IO;
using System.Linq;

using NUnit.Framework;

using Nav.Language.Mcp.Tests.Infrastructure;

using Pharmatechnik.Nav.Language.Mcp.Tools;

#endregion

namespace Nav.Language.Mcp.Tests;

/// <summary>
/// Tests des MCP-Tools <c>nav_rename</c> (public statische Tool-Methode, direkt aufgerufen). Verifiziert das
/// dateilokale Edit-Set (angewandt ergibt es den erwarteten Text), das <b>Read-only-Versprechen</b> (die Datei
/// auf Platte bleibt byte-identisch), sowie die Fehlerpfade (nicht gefunden / mehrdeutig mit Kandidatenliste).
/// </summary>
[TestFixture]
public class NavRenameToolTests {

    // Ein exit-Knoten 'e1' mit zwei Vorkommen in derselben Datei (Deklaration + Kanten-Ziel) — Basis fürs Umbenennen.
    const string SingleTask =
        """
        task A
        {
            init I1;
            exit e1;
            I1 --> e1;
        }
        """;

    // Zwei Tasks mit gleichnamigem Knoten 'Start' — macht 'Start' mehrdeutig (je einmal in A und B).
    const string TwoTasksSharedNode =
        """
        task A
        {
            init Start;
            exit Done;
            Start --> Done;
        }
        task B
        {
            init Start;
            exit Done;
            Start --> Done;
        }
        """;

    [Test]
    public void Rename_Node_ProducesFileLocalEditsThatYieldExpectedText() {

        using var ws   = new McpTestWorkspace();
        var       path = ws.WriteFile("a.nav", SingleTask);

        var result = NavRenameTool.Rename(ws.Workspace, path, name: "e1", newName: "e2");

        Assert.IsNull(result.Error);
        CollectionAssert.IsNotEmpty(result.Edits, "Ein umbenennbarer Knoten liefert Edits.");

        // Das angewandte Edit-Set ersetzt beide Vorkommen von 'e1' (Deklaration + Kanten-Ziel) durch 'e2'.
        var applied = EditApplier.Apply(File.ReadAllText(path), result.Edits);
        StringAssert.Contains("exit e2;",   applied);
        StringAssert.Contains("I1 --> e2;", applied);
        Assert.IsFalse(applied.Contains("e1"), "Kein 'e1' bleibt übrig.");
    }

    [Test]
    public void Rename_DoesNotTouchTheFileOnDisk() {

        using var ws     = new McpTestWorkspace();
        var       path   = ws.WriteFile("a.nav", SingleTask);
        var       before = File.ReadAllText(path);

        NavRenameTool.Rename(ws.Workspace, path, name: "e1", newName: "e2");

        // Read-only-Versprechen: nav_rename liefert nur das Edit-Set und schreibt selbst nichts.
        Assert.AreEqual(before, File.ReadAllText(path), "nav_rename darf die Datei auf Platte nicht verändern.");
    }

    [Test]
    public void Rename_UnknownName_ReportsNotFound() {

        using var ws   = new McpTestWorkspace();
        var       path = ws.WriteFile("a.nav", SingleTask);

        var result = NavRenameTool.Rename(ws.Workspace, path, name: "DoesNotExist", newName: "Whatever");

        Assert.AreEqual(NavNameResolution.NotFoundMessage("DoesNotExist", path), result.Error);
        CollectionAssert.IsEmpty(result.Edits);
    }

    [Test]
    public void Rename_AmbiguousName_ReturnsCandidatesWithKindAndTask() {

        using var ws   = new McpTestWorkspace();
        var       path = ws.WriteFile("a.nav", TwoTasksSharedNode);

        var result = NavRenameTool.Rename(ws.Workspace, path, name: "Start", newName: "Begin");

        Assert.AreEqual(NavNameResolution.AmbiguousMessage("Start"), result.Error);
        Assert.AreEqual(2,                                           result.Candidates.Count, "'Start' ist in Task A und B je einmal deklariert.");
        CollectionAssert.IsEmpty(result.Edits, "Bei Mehrdeutigkeit gibt es kein Edit-Set.");

        // Jeder Kandidat trägt seine Art und die enthaltende Task — die Achsen zum Eingrenzen.
        Assert.IsTrue(result.Candidates.All(c => c.Kind == "init"));
        CollectionAssert.AreEquivalent(new[] { "A", "B" }, result.Candidates.Select(c => c.Task));
    }

}
