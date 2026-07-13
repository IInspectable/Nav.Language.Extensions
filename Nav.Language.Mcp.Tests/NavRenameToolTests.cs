#region Using Directives

using System.IO;
using System.Linq;
using System.Threading.Tasks;

using NUnit.Framework;

using Nav.Language.Mcp.Tests.Infrastructure;

using Pharmatechnik.Nav.Language.Mcp.Tools;

#endregion

namespace Nav.Language.Mcp.Tests;

/// <summary>
/// Tests des MCP-Tools <c>nav_rename</c> (public statische Tool-Methode, direkt aufgerufen). Verifiziert das
/// dateilokale Edit-Set (angewandt ergibt es den erwarteten Text), das <b>Read-only-Versprechen</b> (die Datei
/// auf Platte bleibt byte-identisch), die Cross-File-Warnung (Task-Name/Exit sind über Dateigrenzen sichtbar)
/// sowie die Fehlerpfade (nicht gefunden / mehrdeutig mit Kandidatenliste).
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

    // Bibliotheksdatei mit der Task 'Sub' und deren Exit 'x'.
    const string Lib =
        """
        task Sub
        {
            init I;
            exit x;
            I --> x;
        }
        """;

    // Instanziiert 'Sub' (cross-file) und benutzt dessen Exit 'x' über die Kante 's:x --> e'.
    const string Main =
        """
        taskref "lib.nav";
        task M
        {
            init I;
            task Sub s;
            exit e;
            I   --> s;
            s:x --> e;
        }
        """;

    [Test]
    public async Task Rename_Node_ProducesFileLocalEditsThatYieldExpectedText() {

        using var ws   = new McpTestWorkspace();
        var       path = ws.WriteFile("a.nav", SingleTask);

        var result = await NavRenameTool.Rename(ws.Workspace, path, name: "e1", newName: "e2");

        Assert.IsNull(result.Error);
        CollectionAssert.IsNotEmpty(result.Edits, "Ein umbenennbarer Knoten liefert Edits.");

        // Das angewandte Edit-Set ersetzt beide Vorkommen von 'e1' (Deklaration + Kanten-Ziel) durch 'e2'.
        var applied = EditApplier.Apply(File.ReadAllText(path), result.Edits);
        StringAssert.Contains("exit e2;",   applied);
        StringAssert.Contains("I1 --> e2;", applied);
        Assert.IsFalse(applied.Contains("e1"), "Kein 'e1' bleibt übrig.");
    }

    [Test]
    public async Task Rename_ResultText_IsTheWholeFileWithEditsApplied() {

        using var ws   = new McpTestWorkspace();
        var       path = ws.WriteFile("a.nav", SingleTask);

        var result = await NavRenameTool.Rename(ws.Workspace, path, name: "e1", newName: "e2");

        Assert.IsNull(result.Error);
        Assert.IsNotNull(result.ResultText, "Standardmäßig liefert nav_rename den kompletten Ergebnistext mit.");

        // Der Voll-Text ist exakt die Datei mit angewandtem Edit-Set — der Agent kann die Datei damit
        // überschreiben, statt die Edits punktgenau selbst zu spleißen.
        Assert.AreEqual(EditApplier.Apply(File.ReadAllText(path), result.Edits), result.ResultText);
        StringAssert.Contains("exit e2;",   result.ResultText);
        StringAssert.Contains("I1 --> e2;", result.ResultText);
    }

    [Test]
    public async Task Rename_IncludeResultTextFalse_OmitsResultTextButKeepsEdits() {

        using var ws   = new McpTestWorkspace();
        var       path = ws.WriteFile("a.nav", SingleTask);

        var result = await NavRenameTool.Rename(ws.Workspace, path, name: "e1", newName: "e2", includeResultText: false);

        Assert.IsNull(result.Error);
        Assert.IsNull(result.ResultText, "Mit includeResultText=false bleibt der Voll-Text weg.");
        CollectionAssert.IsNotEmpty(result.Edits, "Die Edits bleiben aber erhalten.");
    }

    [Test]
    public async Task Rename_DoesNotTouchTheFileOnDisk() {

        using var ws     = new McpTestWorkspace();
        var       path   = ws.WriteFile("a.nav", SingleTask);
        var       before = File.ReadAllText(path);

        await NavRenameTool.Rename(ws.Workspace, path, name: "e1", newName: "e2");

        // Read-only-Versprechen: nav_rename liefert nur das Edit-Set und schreibt selbst nichts.
        Assert.AreEqual(before, File.ReadAllText(path), "nav_rename darf die Datei auf Platte nicht verändern.");
    }

    [Test]
    public async Task Rename_LocalOnlySymbol_HasNoCrossFileWarning() {

        using var ws   = new McpTestWorkspace();
        var       path = ws.WriteFile("a.nav", SingleTask);

        var result = await NavRenameTool.Rename(ws.Workspace, path, name: "e1", newName: "e2");

        Assert.IsNull(result.Error);
        Assert.IsNull(result.Warning, "Ein rein dateilokaler Knoten löst keine Cross-File-Warnung aus.");
        Assert.AreEqual(0, result.CrossFileReferenceCount);
        CollectionAssert.IsEmpty(result.CrossFileFiles);
    }

    [Test]
    public async Task Rename_TaskName_WarnsAboutCrossFileCallers() {

        using var ws      = new McpTestWorkspace();
        var       libPath = ws.WriteFile("lib.nav", Lib);
        ws.WriteFile("main.nav", Main);

        var result = await NavRenameTool.Rename(ws.Workspace, libPath, name: "Sub", newName: "SubX");

        Assert.IsNull(result.Error);
        CollectionAssert.IsNotEmpty(result.Edits, "Der Rename in der Definitionsdatei bleibt dateilokal vorhanden.");

        Assert.IsNotNull(result.Warning, "Der task-Knoten 'Sub s' in main.nav bricht sonst still.");
        Assert.GreaterOrEqual(result.CrossFileReferenceCount, 1);
        Assert.AreEqual(1, result.CrossFileFiles.Count);
        StringAssert.EndsWith("main.nav", result.CrossFileFiles[0].ToLowerInvariant());
    }

    [Test]
    public async Task Rename_ExitUsedFromInstance_WarnsAboutCrossFileEdges() {

        using var ws      = new McpTestWorkspace();
        var       libPath = ws.WriteFile("lib.nav", Lib);
        ws.WriteFile("main.nav", Main);

        // Exit 'x' der Task 'Sub' umbenennen — die Kante 's:x --> e' in main.nav bricht sonst still.
        var result = await NavRenameTool.Rename(ws.Workspace, libPath, name: "x", newName: "y", task: "Sub");

        Assert.IsNull(result.Error);
        CollectionAssert.IsNotEmpty(result.Edits);

        Assert.IsNotNull(result.Warning);
        Assert.GreaterOrEqual(result.CrossFileReferenceCount, 1);
        Assert.AreEqual(1, result.CrossFileFiles.Count);
        StringAssert.EndsWith("main.nav", result.CrossFileFiles[0].ToLowerInvariant());
    }

    [Test]
    public async Task Rename_UnknownName_ReportsNotFound() {

        using var ws   = new McpTestWorkspace();
        var       path = ws.WriteFile("a.nav", SingleTask);

        var result = await NavRenameTool.Rename(ws.Workspace, path, name: "DoesNotExist", newName: "Whatever");

        Assert.AreEqual(NavNameResolution.NotFoundMessage("DoesNotExist", path), result.Error);
        CollectionAssert.IsEmpty(result.Edits);
    }

    [Test]
    public async Task Rename_AmbiguousName_ReturnsCandidatesWithKindAndTask() {

        using var ws   = new McpTestWorkspace();
        var       path = ws.WriteFile("a.nav", TwoTasksSharedNode);

        var result = await NavRenameTool.Rename(ws.Workspace, path, name: "Start", newName: "Begin");

        Assert.AreEqual(NavNameResolution.AmbiguousMessage("Start"), result.Error);
        Assert.AreEqual(2,                                           result.Candidates.Count, "'Start' ist in Task A und B je einmal deklariert.");
        CollectionAssert.IsEmpty(result.Edits, "Bei Mehrdeutigkeit gibt es kein Edit-Set.");

        // Jeder Kandidat trägt seine Art und die enthaltende Task — die Achsen zum Eingrenzen.
        Assert.IsTrue(result.Candidates.All(c => c.Kind == "init"));
        CollectionAssert.AreEquivalent(new[] { "A", "B" }, result.Candidates.Select(c => c.Task));
    }

}
