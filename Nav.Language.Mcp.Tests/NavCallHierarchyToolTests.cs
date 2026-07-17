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

        var result = await NavCallHierarchyTool.CallHierarchy(ws.Workspace, mainPath, task: "M", direction: "outgoing", detail: "full");

        Assert.IsNull(result.Error);
        Assert.AreEqual(1, result.Outgoing.Count);
        Assert.AreEqual(1, result.CalleeCount, "Eine aufgerufene Task.");

        var call = result.Outgoing[0];
        Assert.AreEqual("Sub", call.Task);
        StringAssert.EndsWith("lib.nav", call.Location.File.ToLowerInvariant(), "Das Ziel liegt in der inkludierten Datei.");
        Assert.AreEqual(1, call.CallSiteCount, "Eine Aufrufstelle.");
        Assert.AreEqual(1, call.CallSites.Count, "detail=full listet die Aufrufstelle.");
        Assert.Greater(call.CallSites[0].Line, 0, "Die Aufrufstelle trägt eine 1-basierte Position (Datei = die abgefragte main.nav).");

        CollectionAssert.IsEmpty(result.Incoming, "Bei direction=outgoing bleibt Incoming leer.");
    }

    [Test]
    public async Task CallHierarchy_Incoming_FindsCallerCrossFile() {

        using var ws = new McpTestWorkspace();
        var libPath = ws.WriteFile("lib.nav", Lib);
        ws.WriteFile("main.nav", Main);

        var result = await NavCallHierarchyTool.CallHierarchy(ws.Workspace, libPath, task: "Sub", direction: "incoming", detail: "full");

        Assert.IsNull(result.Error);
        Assert.AreEqual(1, result.Incoming.Count);
        Assert.AreEqual(1, result.CallerCount, "Ein Aufrufer.");
        Assert.AreEqual(1, result.CallSiteCount, "Eine Aufrufstelle solution-weit.");

        var call = result.Incoming[0];
        Assert.AreEqual("M", call.Task);
        StringAssert.EndsWith("main.nav", call.Location.File.ToLowerInvariant(), "Der Aufrufer ist in main.nav definiert.");
        Assert.AreEqual(1, call.CallSiteCount);
        Assert.AreEqual(1, call.CallSites.Count, "detail=full listet die Aufrufstelle.");

        CollectionAssert.IsEmpty(result.Outgoing, "Bei direction=incoming bleibt Outgoing leer.");
    }

    [Test]
    public async Task CallHierarchy_Both_LeafTaskHasCallerButNoCallees() {

        using var ws = new McpTestWorkspace();
        var libPath = ws.WriteFile("lib.nav", Lib);
        ws.WriteFile("main.nav", Main);

        var result = await NavCallHierarchyTool.CallHierarchy(ws.Workspace, libPath, task: "Sub", direction: "both");

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

        var result = await NavCallHierarchyTool.CallHierarchy(ws.Workspace, mainPath, task: "M", direction: "outgoing", detail: "full");

        Assert.IsNull(result.Error);
        Assert.AreEqual(1, result.Outgoing.Count, "Zwei Aufrufe derselben Task → eine Beziehung.");
        Assert.AreEqual("Sub", result.Outgoing[0].Task);
        Assert.AreEqual(2, result.Outgoing[0].CallSiteCount, "…mit zwei Aufrufstellen.");
        Assert.AreEqual(2, result.Outgoing[0].CallSites.Count, "detail=full listet beide Aufrufstellen.");
    }

    [Test]
    public async Task CallHierarchy_InvalidDirection_ReturnsError() {

        using var ws   = new McpTestWorkspace();
        var       path = ws.WriteFile("a.nav", Lib);

        var result = await NavCallHierarchyTool.CallHierarchy(ws.Workspace, path, task: "Sub", direction: "sideways");

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

    [Test]
    public async Task CallHierarchy_SummaryDefault_OmitsCallSitesButKeepsCount() {

        using var ws = new McpTestWorkspace();
        ws.WriteFile("lib.nav", Lib);
        var mainPath = ws.WriteFile("main.nav", MainTwoCalls);

        // Ohne detail-Argument → Default 'summary'.
        var result = await NavCallHierarchyTool.CallHierarchy(ws.Workspace, mainPath, task: "M", direction: "outgoing");

        Assert.IsNull(result.Error);
        Assert.AreEqual(1, result.Outgoing.Count);
        Assert.AreEqual(2, result.Outgoing[0].CallSiteCount, "Die Anzahl steht auch im summary-Modus.");
        CollectionAssert.IsEmpty(result.Outgoing[0].CallSites, "summary listet die einzelnen Aufrufstellen NICHT.");
    }

    [Test]
    public async Task CallHierarchy_InvalidDetail_ReturnsError() {

        using var ws   = new McpTestWorkspace();
        var       path = ws.WriteFile("a.nav", Lib);

        var result = await NavCallHierarchyTool.CallHierarchy(ws.Workspace, path, task: "Sub", detail: "verbose");

        Assert.IsNotNull(result.Error);
        StringAssert.Contains("detail", result.Error);
        CollectionAssert.IsEmpty(result.Outgoing);
        CollectionAssert.IsEmpty(result.Incoming);
    }

    [Test]
    public async Task CallHierarchy_Incoming_FilterScopesByCallerFilePath() {

        using var ws = new McpTestWorkspace();
        var libPath = ws.WriteFile("lib.nav", Lib);
        ws.WriteFile("alpha.nav", Main);
        ws.WriteFile("beta.nav",  Main);

        var all = await NavCallHierarchyTool.CallHierarchy(ws.Workspace, libPath, task: "Sub", direction: "incoming");
        Assert.IsNull(all.Error);
        Assert.AreEqual(2, all.CallerCount, "Zwei Aufrufer solution-weit.");
        Assert.AreEqual(2, all.MatchCount);

        var filtered = await NavCallHierarchyTool.CallHierarchy(
            ws.Workspace, libPath, task: "Sub", direction: "incoming", filter: "alpha");

        Assert.IsNull(filtered.Error);
        Assert.AreEqual(2, filtered.CallerCount, "CallerCount bleibt der solution-weite Gesamtwert (vor Filter).");
        Assert.AreEqual(1, filtered.MatchCount, "Nur ein Aufrufer erfüllt den Filter.");
        Assert.AreEqual(1, filtered.Incoming.Count);
        StringAssert.EndsWith("alpha.nav", filtered.Incoming[0].Location.File.ToLowerInvariant());
        Assert.IsFalse(filtered.Truncated);
    }

    [Test]
    public async Task CallHierarchy_Incoming_LimitAndOffsetPageThroughCallers() {

        using var ws = new McpTestWorkspace();
        var libPath = ws.WriteFile("lib.nav", Lib);
        ws.WriteFile("c1.nav", Main);
        ws.WriteFile("c2.nav", Main);
        ws.WriteFile("c3.nav", Main);

        var firstPage = await NavCallHierarchyTool.CallHierarchy(
            ws.Workspace, libPath, task: "Sub", direction: "incoming", limit: 2, offset: 0);

        Assert.IsNull(firstPage.Error);
        Assert.AreEqual(3, firstPage.CallerCount);
        Assert.AreEqual(3, firstPage.MatchCount);
        Assert.AreEqual(2, firstPage.Returned);
        Assert.AreEqual(2, firstPage.Incoming.Count);
        Assert.AreEqual(2, firstPage.Limit);
        Assert.AreEqual(0, firstPage.Offset);
        Assert.IsTrue(firstPage.Truncated, "Es gibt einen dritten Aufrufer jenseits dieser Seite.");

        var secondPage = await NavCallHierarchyTool.CallHierarchy(
            ws.Workspace, libPath, task: "Sub", direction: "incoming", limit: 2, offset: 2);

        Assert.IsNull(secondPage.Error);
        Assert.AreEqual(1, secondPage.Returned, "Der verbleibende dritte Aufrufer.");
        Assert.IsFalse(secondPage.Truncated, "Danach ist Schluss.");
    }

}
