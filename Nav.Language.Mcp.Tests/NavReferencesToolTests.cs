#region Using Directives

using System.Linq;
using System.Threading.Tasks;

using NUnit.Framework;

using Nav.Language.Mcp.Tests.Infrastructure;

using Pharmatechnik.Nav.Language.Mcp.Tools;

#endregion

namespace Nav.Language.Mcp.Tests;

/// <summary>
/// Tests des MCP-Tools <c>nav_references</c> (public statische Tool-Methode, direkt aufgerufen). Verifiziert die
/// solution-weite Vorkommen-Suche inkl. der Deklaration (<c>isDeclaration</c>), den pfadbasierten <c>filter</c>,
/// das Paging sowie die Mehrdeutigkeits-Kandidaten. Gegen einen Temp-Workspace mit hingeschriebenen Fixtures.
/// </summary>
[TestFixture]
public class NavReferencesToolTests {

    // 'Sub' wird in lib.nav definiert und in main.nav (per Include) als Knoten-Task genutzt — die Referenzen
    // verteilen sich cross-file, die Deklaration liegt in lib.nav.
    const string Lib =
        """
        task Sub
        {
            init I;
            exit x;
            I --> x;
        }
        """;

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
    public async Task References_IncludeDeclaration_AcrossFiles() {

        using var ws      = new McpTestWorkspace();
        var       libPath = ws.WriteFile("lib.nav", Lib);
        ws.WriteFile("main.nav", Main);

        var result = await NavReferencesTool.References(ws.Workspace, libPath, name: "Sub");

        Assert.IsNull(result.Error);

        // Genau eine der Fundstellen ist die Deklaration.
        Assert.AreEqual(1, result.Locations.Count(l => l.IsDeclaration), "Die Deklaration ist genau einmal dabei.");

        // Es gibt mindestens die Deklaration in lib.nav und die Nutzung in main.nav → cross-file.
        Assert.IsTrue(result.Locations.Any(l => l.File.ToLowerInvariant().EndsWith("main.nav")),
                      "Die Nutzung in main.nav ist enthalten.");
        Assert.GreaterOrEqual(result.Count, 2);
    }

    [Test]
    public async Task References_Filter_ScopesToFile() {

        using var ws      = new McpTestWorkspace();
        var       libPath = ws.WriteFile("lib.nav", Lib);
        ws.WriteFile("main.nav", Main);

        var result = await NavReferencesTool.References(ws.Workspace, libPath, name: "Sub", filter: "main.nav");

        Assert.IsTrue(result.Locations.All(l => l.File.ToLowerInvariant().Contains("main.nav")),
                      "Der Filter grenzt auf main.nav ein.");
        Assert.Less(result.MatchCount, result.Count, "Gefiltert bleiben weniger Treffer als insgesamt.");
    }

    [Test]
    public async Task References_Paging_Truncates() {

        using var ws      = new McpTestWorkspace();
        var       libPath = ws.WriteFile("lib.nav", Lib);
        ws.WriteFile("main.nav", Main);

        // Erste Seite auf Größe 1 begrenzt → es folgen noch weitere Vorkommen.
        var firstPage = await NavReferencesTool.References(ws.Workspace, libPath, name: "Sub", limit: 1, offset: 0);

        Assert.AreEqual(1, firstPage.Returned);
        Assert.IsTrue(firstPage.Truncated, "Bei mehr als einem Vorkommen ist die erste 1er-Seite truncated.");
        Assert.GreaterOrEqual(firstPage.Count, 2);
    }

    [Test]
    public async Task References_AmbiguousName_ReturnsCandidates() {

        using var ws   = new McpTestWorkspace();
        var       path = ws.WriteFile("a.nav", TwoTasksSharedNode);

        var result = await NavReferencesTool.References(ws.Workspace, path, name: "Start");

        Assert.AreEqual(NavNameResolution.AmbiguousMessage("Start"), result.Error);
        Assert.AreEqual(2,                                           result.Candidates.Count);
        CollectionAssert.IsEmpty(result.Locations);
    }

    [Test]
    public async Task References_UnknownName_ReportsNotFound() {

        using var ws   = new McpTestWorkspace();
        var       path = ws.WriteFile("a.nav", TwoTasksSharedNode);

        var result = await NavReferencesTool.References(ws.Workspace, path, name: "DoesNotExist");

        Assert.AreEqual(NavNameResolution.NotFoundMessage("DoesNotExist", path), result.Error);
    }

}
