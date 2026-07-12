#region Using Directives

using System.IO;
using System.Linq;

using NUnit.Framework;

using Nav.Language.Mcp.Tests.Infrastructure;

using Pharmatechnik.Nav.Language.Mcp.Tools;

#endregion

namespace Nav.Language.Mcp.Tests;

/// <summary>
/// Tests des MCP-Tools <c>nav_code_actions</c> (public statische Tool-Methode, direkt aufgerufen). Verifiziert
/// die am Symbol anwendbaren Aktionen samt fertiger Edits (angewandt ergeben sie den erwarteten Text), das
/// <b>Read-only-Versprechen</b> (Datei auf Platte byte-identisch) und die Fehlerpfade (nicht gefunden /
/// mehrdeutig mit Kandidatenliste).
/// </summary>
[TestFixture]
public class NavCodeActionsToolTests {

    // Ein ungenutzter view-Knoten 'v' — bietet die Quick-Fix-Aktion 'Remove Unused Nodes' an.
    const string UnusedViewNode =
        """
        task A
        {
            init I1;
            exit e1;
            view v;

            I1 --> e1;
        }
        """;

    // Zwei Tasks mit gleichnamigem Knoten 'Start' — 'Start' ist damit mehrdeutig.
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
    public void CodeActions_UnusedNode_OffersRemoveWithApplicableEdits() {

        using var ws   = new McpTestWorkspace();
        var       path = ws.WriteFile("a.nav", UnusedViewNode);

        var result = NavCodeActionsTool.CodeActions(ws.Workspace, path, name: "v");

        Assert.IsNull(result.Error);

        var remove = result.Actions.SingleOrDefault(a => a.Title == "Remove Unused Nodes");
        Assert.IsNotNull(remove, "Für den ungenutzten Knoten wird 'Remove Unused Nodes' angeboten.");
        CollectionAssert.IsNotEmpty(remove!.Edits);

        // Das angewandte Edit-Set entfernt den ungenutzten Knoten; der Rest bleibt stehen.
        var applied = EditApplier.Apply(File.ReadAllText(path), remove.Edits);
        Assert.IsFalse(applied.Contains("view v;"), "Der ungenutzte Knoten ist entfernt.");
        StringAssert.Contains("I1 --> e1;", applied);
    }

    [Test]
    public void CodeActions_DoesNotTouchTheFileOnDisk() {

        using var ws     = new McpTestWorkspace();
        var       path   = ws.WriteFile("a.nav", UnusedViewNode);
        var       before = File.ReadAllText(path);

        NavCodeActionsTool.CodeActions(ws.Workspace, path, name: "v");

        Assert.AreEqual(before, File.ReadAllText(path), "nav_code_actions darf die Datei auf Platte nicht verändern.");
    }

    [Test]
    public void CodeActions_UnknownName_ReportsNotFound() {

        using var ws   = new McpTestWorkspace();
        var       path = ws.WriteFile("a.nav", UnusedViewNode);

        var result = NavCodeActionsTool.CodeActions(ws.Workspace, path, name: "DoesNotExist");

        Assert.AreEqual(NavNameResolution.NotFoundMessage("DoesNotExist", path), result.Error);
        CollectionAssert.IsEmpty(result.Actions);
    }

    [Test]
    public void CodeActions_AmbiguousName_ReturnsCandidatesWithKindAndTask() {

        using var ws   = new McpTestWorkspace();
        var       path = ws.WriteFile("a.nav", TwoTasksSharedNode);

        var result = NavCodeActionsTool.CodeActions(ws.Workspace, path, name: "Start");

        Assert.AreEqual(NavNameResolution.AmbiguousMessage("Start"), result.Error);
        Assert.AreEqual(2,                                           result.Candidates.Count);
        CollectionAssert.IsEmpty(result.Actions, "Bei Mehrdeutigkeit gibt es keine Aktionen.");

        Assert.IsTrue(result.Candidates.All(c => c.Kind == "init"));
        CollectionAssert.AreEquivalent(new[] { "A", "B" }, result.Candidates.Select(c => c.Task));
    }

}
