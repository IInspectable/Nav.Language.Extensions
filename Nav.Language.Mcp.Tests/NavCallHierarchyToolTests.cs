#region Using Directives

using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

using Nav.Language.Mcp.Tests.Infrastructure;

using Pharmatechnik.Nav.Language.Mcp.Tools;

#endregion

namespace Nav.Language.Mcp.Tests;

/// <summary>
/// Tests des MCP-Tools <c>nav_call_hierarchy</c> (public statische Tool-Methode, direkt aufgerufen).
/// Verifiziert die Task-Ebenen-Aufrufbeziehungen ausgehend/eingehend, cross-file via <c>taskref</c>, die
/// Gruppierung mehrerer Aufrufstellen, den Richtungsfilter sowie die Fehlerpfade. Gegen einen Temp-Workspace
/// mit hingeschriebenen <c>.nav</c>-Fixtures.
/// </summary>
[TestFixture]
public class NavCallHierarchyToolTests {

    // Bibliotheksdatei mit der aufgerufenen Task 'Sub'.
    const string Lib =
        """
        task Sub
        {
            init I;
            exit x;
            I --> x;
        }
        """;

    // Ruft 'Sub' (cross-file) über einen task-Knoten auf.
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

    // Ruft 'Sub' zweimal auf — prüft die Gruppierung nach Ziel (eine Beziehung, zwei Aufrufstellen).
    const string MainTwoCalls =
        """
        taskref "lib.nav";
        task M
        {
            init I;
            task Sub s1;
            task Sub s2;
            exit e;
            I    --> s1;
            s1:x --> s2;
            s2:x --> e;
        }
        """;

    [Test]
    public async Task CallHierarchy_Outgoing_ListsCalledTaskCrossFile() {

        using var ws = new McpTestWorkspace();
        ws.WriteFile("lib.nav", Lib);
        var mainPath = ws.WriteFile("main.nav", Main);

        var result = await NavCallHierarchyTool.CallHierarchy(ws.Workspace, mainPath, task: "M", direction: "outgoing", CancellationToken.None);

        Assert.IsNull(result.Error);
        Assert.AreEqual(1, result.Outgoing.Count);

        var call = result.Outgoing[0];
        Assert.AreEqual("Sub", call.Task);
        StringAssert.EndsWith("lib.nav", call.Location.File.ToLowerInvariant(), "Das Ziel liegt in der inkludierten Datei.");
        Assert.AreEqual(1, call.CallSites.Count);
        StringAssert.EndsWith("main.nav", call.CallSites[0].File.ToLowerInvariant(), "Die Aufrufstelle steht in der aufrufenden Datei.");

        CollectionAssert.IsEmpty(result.Incoming, "Bei direction=outgoing bleibt Incoming leer.");
    }

    [Test]
    public async Task CallHierarchy_Incoming_FindsCallerCrossFile() {

        using var ws = new McpTestWorkspace();
        var libPath = ws.WriteFile("lib.nav", Lib);
        ws.WriteFile("main.nav", Main);

        var result = await NavCallHierarchyTool.CallHierarchy(ws.Workspace, libPath, task: "Sub", direction: "incoming", CancellationToken.None);

        Assert.IsNull(result.Error);
        Assert.AreEqual(1, result.Incoming.Count);

        var call = result.Incoming[0];
        Assert.AreEqual("M", call.Task);
        StringAssert.EndsWith("main.nav", call.Location.File.ToLowerInvariant(), "Der Aufrufer ist in main.nav definiert.");
        Assert.AreEqual(1, call.CallSites.Count);

        CollectionAssert.IsEmpty(result.Outgoing, "Bei direction=incoming bleibt Outgoing leer.");
    }

    [Test]
    public async Task CallHierarchy_Both_LeafTaskHasCallerButNoCallees() {

        using var ws = new McpTestWorkspace();
        var libPath = ws.WriteFile("lib.nav", Lib);
        ws.WriteFile("main.nav", Main);

        var result = await NavCallHierarchyTool.CallHierarchy(ws.Workspace, libPath, task: "Sub", direction: "both", CancellationToken.None);

        Assert.IsNull(result.Error);
        Assert.AreEqual("both", result.Direction);
        Assert.AreEqual(1, result.Incoming.Count, "'Sub' wird von 'M' aufgerufen.");
        CollectionAssert.IsEmpty(result.Outgoing, "'Sub' ruft selbst keine Task auf.");
    }

    [Test]
    public async Task CallHierarchy_GroupsMultipleCallSitesByTarget() {

        using var ws = new McpTestWorkspace();
        ws.WriteFile("lib.nav", Lib);
        var mainPath = ws.WriteFile("main.nav", MainTwoCalls);

        var result = await NavCallHierarchyTool.CallHierarchy(ws.Workspace, mainPath, task: "M", direction: "outgoing", CancellationToken.None);

        Assert.IsNull(result.Error);
        Assert.AreEqual(1, result.Outgoing.Count, "Zwei Aufrufe derselben Task → eine Beziehung.");
        Assert.AreEqual("Sub", result.Outgoing[0].Task);
        Assert.AreEqual(2, result.Outgoing[0].CallSites.Count, "…mit zwei Aufrufstellen.");
    }

    [Test]
    public async Task CallHierarchy_InvalidDirection_ReturnsError() {

        using var ws   = new McpTestWorkspace();
        var       path = ws.WriteFile("a.nav", Lib);

        var result = await NavCallHierarchyTool.CallHierarchy(ws.Workspace, path, task: "Sub", direction: "sideways", CancellationToken.None);

        Assert.IsNotNull(result.Error);
        StringAssert.Contains("direction", result.Error);
    }

    [Test]
    public async Task CallHierarchy_UnknownTask_ReturnsError() {

        using var ws   = new McpTestWorkspace();
        var       path = ws.WriteFile("a.nav", Lib);

        var result = await NavCallHierarchyTool.CallHierarchy(ws.Workspace, path, task: "Nope");

        Assert.IsNotNull(result.Error);
        CollectionAssert.IsEmpty(result.Outgoing);
        CollectionAssert.IsEmpty(result.Incoming);
    }

    [Test]
    public async Task CallHierarchy_MissingFile_ReturnsNotFound() {

        using var ws = new McpTestWorkspace();

        var result = await NavCallHierarchyTool.CallHierarchy(
            ws.Workspace, System.IO.Path.Combine(ws.Root, "nope.nav"), task: "Sub");

        Assert.IsNotNull(result.Error);
    }

}
