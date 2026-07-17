#region Using Directives

using System.Threading.Tasks;

using NUnit.Framework;

using Nav.Language.Mcp.Tests.Infrastructure;

using Pharmatechnik.Nav.Language.Mcp.Tools;

#endregion

namespace Nav.Language.Mcp.Tests;

/// <summary>
/// Tests des MCP-Tools <c>nav_exit_usages</c> (public statische Tool-Methode, direkt aufgerufen). Verifiziert
/// die solution-weite Auflösung der <c>Instanz:Exit --&gt; …</c>-Kanten (cross-file via <c>taskref</c>), die
/// Gruppierung nach aufrufender Task, den Exit-Namens-Scope, den Datei-Filter, das Paging sowie die
/// Fehlerpfade. Gegen einen Temp-Workspace mit hingeschriebenen <c>.nav</c>-Fixtures.
/// </summary>
[TestFixture]
public class NavExitUsagesToolTests {

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
    public async Task ExitUsages_CrossFile_FindsInstanceEdge() {

        using var ws      = new McpTestWorkspace();
        var       libPath = ws.WriteFile("lib.nav", Lib);
        ws.WriteFile("main.nav", Main);

        var result = await NavExitUsagesTool.ExitUsages(ws.Workspace, libPath, task: "Sub", exit: "x");

        Assert.IsNull(result.Error);
        Assert.AreEqual(1, result.CallerCount, "Ein Aufrufer benutzt den Exit.");
        Assert.AreEqual(1, result.SiteCount);
        Assert.AreEqual(1, result.Usages.Count);

        var usage = result.Usages[0];
        Assert.AreEqual("M", usage.Caller);
        StringAssert.EndsWith("main.nav", usage.Location.File.ToLowerInvariant(),
                              "Die Exit-Nutzung steht in der aufrufenden Datei, nicht in lib.nav.");
        Assert.AreEqual(1,   usage.SiteCount);
        Assert.AreEqual("x", usage.Sites[0].Exit);
        Assert.AreEqual("s", usage.Sites[0].Instance);
        Assert.Greater(usage.Sites[0].Position.Line, 0, "1-basierte Position der Kante.");
    }

    [Test]
    public async Task ExitUsages_ExitOmitted_ReturnsAllExits() {

        using var ws      = new McpTestWorkspace();
        var       libPath = ws.WriteFile("lib.nav", Lib);
        ws.WriteFile("main.nav", Main);

        // Ohne 'exit'-Argument → Nutzungen aller Exits der Task.
        var result = await NavExitUsagesTool.ExitUsages(ws.Workspace, libPath, task: "Sub");

        Assert.IsNull(result.Error);
        Assert.AreEqual("",  result.Exit, "Echo: kein einzelner Exit angefragt.");
        Assert.AreEqual(1,   result.CallerCount);
        Assert.AreEqual("x", result.Usages[0].Sites[0].Exit);
    }

    [Test]
    public async Task ExitUsages_UnknownExit_ReturnsEmpty() {

        using var ws      = new McpTestWorkspace();
        var       libPath = ws.WriteFile("lib.nav", Lib);
        ws.WriteFile("main.nav", Main);

        var result = await NavExitUsagesTool.ExitUsages(ws.Workspace, libPath, task: "Sub", exit: "nope");

        Assert.IsNull(result.Error);
        Assert.AreEqual(0, result.CallerCount);
        CollectionAssert.IsEmpty(result.Usages);
    }

    [Test]
    public async Task ExitUsages_Filter_ScopesByCallerFilePath() {

        using var ws      = new McpTestWorkspace();
        var       libPath = ws.WriteFile("lib.nav", Lib);
        ws.WriteFile("alpha.nav", Main);
        ws.WriteFile("beta.nav",  Main);

        var all = await NavExitUsagesTool.ExitUsages(ws.Workspace, libPath, task: "Sub", exit: "x");
        Assert.IsNull(all.Error);
        Assert.AreEqual(2, all.CallerCount, "Zwei Aufrufer solution-weit.");
        Assert.AreEqual(2, all.MatchCount);

        var filtered = await NavExitUsagesTool.ExitUsages(ws.Workspace, libPath, task: "Sub", exit: "x", filter: "alpha");
        Assert.IsNull(filtered.Error);
        Assert.AreEqual(2, filtered.CallerCount, "CallerCount bleibt der solution-weite Gesamtwert (vor Filter).");
        Assert.AreEqual(1, filtered.MatchCount,  "Nur ein Aufrufer erfüllt den Filter.");
        Assert.AreEqual(1, filtered.Usages.Count);
        StringAssert.EndsWith("alpha.nav", filtered.Usages[0].Location.File.ToLowerInvariant());
        Assert.IsFalse(filtered.Truncated);
    }

    [Test]
    public async Task ExitUsages_LimitAndOffsetPageThroughCallers() {

        using var ws      = new McpTestWorkspace();
        var       libPath = ws.WriteFile("lib.nav", Lib);
        ws.WriteFile("c1.nav", Main);
        ws.WriteFile("c2.nav", Main);
        ws.WriteFile("c3.nav", Main);

        var firstPage = await NavExitUsagesTool.ExitUsages(ws.Workspace, libPath, task: "Sub", exit: "x", limit: 2, offset: 0);
        Assert.IsNull(firstPage.Error);
        Assert.AreEqual(3, firstPage.CallerCount);
        Assert.AreEqual(2, firstPage.Returned);
        Assert.AreEqual(2, firstPage.Usages.Count);
        Assert.IsTrue(firstPage.Truncated, "Es gibt einen dritten Aufrufer jenseits dieser Seite.");

        var secondPage = await NavExitUsagesTool.ExitUsages(ws.Workspace, libPath, task: "Sub", exit: "x", limit: 2, offset: 2);
        Assert.IsNull(secondPage.Error);
        Assert.AreEqual(1, secondPage.Returned, "Der verbleibende dritte Aufrufer.");
        Assert.IsFalse(secondPage.Truncated, "Danach ist Schluss.");
    }

    [Test]
    public async Task ExitUsages_UnknownTask_ReturnsError() {

        using var ws   = new McpTestWorkspace();
        var       path = ws.WriteFile("a.nav", Lib);

        var result = await NavExitUsagesTool.ExitUsages(ws.Workspace, path, task: "Nope");

        Assert.IsNotNull(result.Error);
        CollectionAssert.IsEmpty(result.Usages);
    }

    [Test]
    public async Task ExitUsages_MissingFile_ReturnsNotFound() {

        using var ws = new McpTestWorkspace();

        var result = await NavExitUsagesTool.ExitUsages(
            ws.Workspace, System.IO.Path.Combine(ws.Root, "nope.nav"), task: "Sub");

        Assert.IsNotNull(result.Error);
    }

}
