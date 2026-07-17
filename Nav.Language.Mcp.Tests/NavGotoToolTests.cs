#region Using Directives

using System.Linq;

using NUnit.Framework;

using Nav.Language.Mcp.Tests.Infrastructure;

using Pharmatechnik.Nav.Language.Mcp.Tools;

#endregion

namespace Nav.Language.Mcp.Tests;

/// <summary>
/// Tests des MCP-Tools <c>nav_goto</c> (public statische Tool-Methode, direkt aufgerufen). Verifiziert die
/// Nav→Nav-Sprünge same-file und cross-file (auf eine Task-Definition in einer inkludierten Datei) sowie die
/// Mehrdeutigkeits-Kandidaten. Gegen einen Temp-Workspace mit hingeschriebenen <c>.nav</c>-Fixtures.
/// </summary>
[TestFixture]
public class NavGotoToolTests {

    // Eine Bibliotheksdatei, deren Task 'Sub' cross-file inkludiert und angesprungen wird.
    const string Lib =
        """
        task Sub
        {
            init I;
            exit x;
            I --> x;
        }
        """;

    // Inkludiert lib.nav und nutzt dessen Task 'Sub' als Knoten — der Task-Typ 'Sub' verweist cross-file.
    const string Main =
        """
        taskref "lib.nav";
        task M
        {
            init I;
            task Sub s;
            exit e;
            I    --> s;
            s:x  --> e;
        }
        """;

    // Zwei Tasks mit gleichnamigem Knoten 'Start' — macht 'Start' mehrdeutig.
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
    public void Goto_SameFileNode_ResolvesWithinTheFile() {

        using var ws   = new McpTestWorkspace();
        var       path = ws.WriteFile("a.nav", TwoTasksSharedNode);

        // 'Done' ist in Task A eindeutig (per task-Scope), der Sprung bleibt in derselben Datei.
        var result = NavGotoTool.Goto(ws.Workspace, path, name: "Done", task: "A");

        Assert.IsNull(result.Error);
        CollectionAssert.IsNotEmpty(result.Locations);
        Assert.IsTrue(result.Locations.All(l => l.File.ToLowerInvariant().EndsWith("a.nav")),
                      "Der Sprung bleibt in derselben Datei.");
    }

    [Test]
    public void Goto_CrossFileTaskNode_ResolvesToIncludedDefinition() {

        using var ws = new McpTestWorkspace();
        ws.WriteFile("lib.nav", Lib);
        var mainPath = ws.WriteFile("main.nav", Main);

        // Der Task-Typ 'Sub' ist über taskref "lib.nav" inkludiert → der Sprung landet in lib.nav.
        var result = NavGotoTool.Goto(ws.Workspace, mainPath, name: "Sub");

        Assert.IsNull(result.Error);
        Assert.AreEqual(1, result.Locations.Count);
        StringAssert.EndsWith("lib.nav", result.Locations[0].File.ToLowerInvariant(),
                              "Die Definition von 'Sub' liegt in der inkludierten Datei.");
    }

    [Test]
    public void Goto_AmbiguousName_ReturnsCandidates() {

        using var ws   = new McpTestWorkspace();
        var       path = ws.WriteFile("a.nav", TwoTasksSharedNode);

        var result = NavGotoTool.Goto(ws.Workspace, path, name: "Start");

        Assert.AreEqual(NavNameResolution.AmbiguousMessage("Start"), result.Error);
        Assert.AreEqual(2,                                           result.Candidates.Count);
        CollectionAssert.IsEmpty(result.Locations, "Bei Mehrdeutigkeit gibt es keine Sprungziele.");
    }

    [Test]
    public void Goto_UnknownName_ReportsNotFound() {

        using var ws   = new McpTestWorkspace();
        var       path = ws.WriteFile("a.nav", TwoTasksSharedNode);

        var result = NavGotoTool.Goto(ws.Workspace, path, name: "DoesNotExist");

        Assert.AreEqual(NavNameResolution.NotFoundMessage("DoesNotExist", path), result.Error);
    }

}
