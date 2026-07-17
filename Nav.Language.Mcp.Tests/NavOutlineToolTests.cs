#region Using Directives

using System.Linq;

using NUnit.Framework;

using Nav.Language.Mcp.Tests.Infrastructure;

using Pharmatechnik.Nav.Language.Mcp.Tools;

#endregion

namespace Nav.Language.Mcp.Tests;

/// <summary>
/// Tests des MCP-Tools <c>nav_outline</c> (public statische Tool-Methode, direkt aufgerufen). Verifiziert die
/// gelieferte Datei-Struktur (Task-Definitionen mit ihren Knoten samt Art und 1-basierter Position) sowie die
/// gemeldete Sprachversion (<c>languageVersion</c>/<c>hasVersionDirective</c>) mit und ohne
/// <c>#version</c>-Direktive. Gegen einen Temp-Workspace mit hingeschriebenen <c>.nav</c>-Fixtures.
/// </summary>
[TestFixture]
public class NavOutlineToolTests {

    const string TaskWithNodes =
        """
        task Login
        {
            init Start;
            dialog Form;
            exit Done;
            Start --> Form;
            Form  --> Done;
        }
        """;

    [Test]
    public void Outline_ListsTasksAndNodesWithKind() {

        using var ws   = new McpTestWorkspace();
        var       path = ws.WriteFile("a.nav", TaskWithNodes);

        var result = NavOutlineTool.Outline(ws.Workspace, path);

        Assert.IsNull(result.Error);
        Assert.AreEqual(1, result.Tasks.Count);

        var task = result.Tasks[0];
        Assert.AreEqual("Login", task.Name);
        Assert.AreEqual(1,       task.Line, "Der Task-Name steht in Zeile 1 (1-basiert).");

        // Die deklarierten Knoten mit ihrer Art.
        var kindByName = task.Nodes.ToDictionary(n => n.Name, n => n.Kind);
        Assert.AreEqual("init", kindByName["Start"]);
        Assert.AreEqual("gui",  kindByName["Form"]);
        Assert.AreEqual("exit", kindByName["Done"]);

        // 1-basierte Position eines Knotens: 'init Start;' steht in Zeile 3.
        var start = task.Nodes.Single(n => n.Name == "Start");
        Assert.AreEqual(3, start.Line);
    }

    [Test]
    public void Outline_WithoutVersionDirective_ReportsDefault() {

        using var ws   = new McpTestWorkspace();
        var       path = ws.WriteFile("a.nav", TaskWithNodes);

        var result = NavOutlineTool.Outline(ws.Workspace, path);

        Assert.IsFalse(result.HasVersionDirective, "Ohne #version gilt der Default.");
        Assert.AreEqual(1, result.LanguageVersion, "Default ist Version 1.");
    }

    [Test]
    public void Outline_WithVersionDirective_ReportsItsVersion() {

        using var ws = new McpTestWorkspace();
        var path = ws.WriteFile("a.nav",
                                """
                                #version 2
                                task A
                                {
                                    init i;
                                    exit e;
                                    i --> e;
                                }
                                """);

        var result = NavOutlineTool.Outline(ws.Workspace, path);

        Assert.IsTrue(result.HasVersionDirective, "Die #version-Direktive ist vorhanden.");
        Assert.AreEqual(2, result.LanguageVersion);
    }

    [Test]
    public void Outline_MissingFile_ReturnsNotFound() {

        using var ws = new McpTestWorkspace();

        var result = NavOutlineTool.Outline(ws.Workspace, System.IO.Path.Combine(ws.Root, "nope.nav"));

        Assert.IsNotNull(result.Error);
        CollectionAssert.IsEmpty(result.Tasks);
    }

}
