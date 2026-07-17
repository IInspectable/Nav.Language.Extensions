#region Using Directives

using System.Linq;
using System.Threading.Tasks;

using NUnit.Framework;

using Nav.Language.Mcp.Tests.Infrastructure;

using Pharmatechnik.Nav.Language.Mcp.Tools;

#endregion

namespace Nav.Language.Mcp.Tests;

/// <summary>
/// Tests des MCP-Tools <c>nav_workspace</c> (public statische Tool-Methode, direkt aufgerufen). Verifiziert die
/// solution-weite Datei-Liste (relative + absolute Pfade), den pfadbasierten <c>filter</c> und die Paging-Kanten.
/// </summary>
[TestFixture]
public class NavWorkspaceToolTests {

    const string Task =
        """
        task A
        {
            init i;
            exit e;
            i --> e;
        }
        """;

    static McpTestWorkspace CreateWorkspace() {
        var ws = new McpTestWorkspace();
        ws.WriteFile("a.nav",     Task);
        ws.WriteFile("sub/b.nav", Task);
        ws.WriteFile("sub/c.nav", Task);
        return ws;
    }

    [Test]
    public async Task Workspace_ListsAllFilesWithRelativeAndAbsolutePaths() {

        using var ws = CreateWorkspace();

        var result = await NavWorkspaceTool.Workspace(ws.Workspace);

        Assert.AreEqual(3, result.FileCount);
        Assert.AreEqual(3, result.Returned);

        // Sortiert nach relativem Pfad (OrdinalIgnoreCase): a.nav vor sub/b.nav vor sub/c.nav.
        var first = result.Files[0];
        StringAssert.EndsWith("a.nav", first.RelativePath.ToLowerInvariant());

        // Jeder Eintrag trägt beide Pfade; der absolute liegt unter der Workspace-Wurzel.
        Assert.IsTrue(result.Files.All(f => !string.IsNullOrEmpty(f.RelativePath)));
        Assert.IsTrue(result.Files.All(f => System.IO.Path.IsPathRooted(f.Path)));
    }

    [Test]
    public async Task Workspace_Filter_ScopesToSubfolderCaseInsensitive() {

        using var ws = CreateWorkspace();

        var result = await NavWorkspaceTool.Workspace(ws.Workspace, filter: "SUB");

        Assert.AreEqual(3, result.FileCount,  "fileCount bleibt die Gesamtzahl, unabhängig vom Filter.");
        Assert.AreEqual(2, result.MatchCount, "Nur die beiden Dateien unter 'sub' passen.");
        Assert.IsTrue(result.Files.All(f => f.RelativePath.ToLowerInvariant().Contains("sub")));
    }

    [Test]
    public async Task Workspace_Paging_TruncatesAndPages() {

        using var ws = CreateWorkspace();

        var firstPage = await NavWorkspaceTool.Workspace(ws.Workspace, limit: 2, offset: 0);
        Assert.AreEqual(3, firstPage.MatchCount);
        Assert.AreEqual(2, firstPage.Returned);
        Assert.IsTrue(firstPage.Truncated);

        var lastPage = await NavWorkspaceTool.Workspace(ws.Workspace, limit: 2, offset: 2);
        Assert.AreEqual(1, lastPage.Returned);
        Assert.AreEqual(2, lastPage.Offset);
        Assert.IsFalse(lastPage.Truncated);
    }

    [Test]
    public async Task Workspace_LimitAndOffset_AreClamped() {

        using var ws = CreateWorkspace();

        var clamped = await NavWorkspaceTool.Workspace(ws.Workspace, limit: 500);
        Assert.AreEqual(200, clamped.Limit);

        var zero = await NavWorkspaceTool.Workspace(ws.Workspace, limit: 0);
        Assert.AreEqual(100, zero.Limit);

        var negativeOffset = await NavWorkspaceTool.Workspace(ws.Workspace, offset: -3);
        Assert.AreEqual(0, negativeOffset.Offset);
    }

}
