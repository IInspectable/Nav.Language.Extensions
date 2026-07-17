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

    // Zwei Tasks, in denen jeweils ein view-Knoten ungenutzt ist ('va' bzw. 'vb') — beide bieten die Aktion
    // 'Remove Unused Nodes' an, aber mit unterschiedlichen Edits.
    const string TwoTasksEachWithUnusedNode =
        """
        task A
        {
            init I1;
            exit e1;
            view va;

            I1 --> e1;
        }
        task B
        {
            init I2;
            exit e2;
            view vb;

            I2 --> e2;
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
    public void CodeActions_ResultText_IsTheWholeFileWithThatActionApplied() {

        using var ws   = new McpTestWorkspace();
        var       path = ws.WriteFile("a.nav", UnusedViewNode);

        var result = NavCodeActionsTool.CodeActions(ws.Workspace, path, name: "v");

        var remove = result.Actions.Single(a => a.Title == "Remove Unused Nodes");
        Assert.IsNotNull(remove.ResultText, "Standardmäßig liefert jede Aktion ihren kompletten Ergebnistext mit.");

        // Der Voll-Text je Aktion ist exakt die Datei mit genau dieser Aktion angewandt.
        Assert.AreEqual(EditApplier.Apply(File.ReadAllText(path), remove.Edits), remove.ResultText);
        Assert.IsFalse(remove.ResultText!.Contains("view v;"), "Der ungenutzte Knoten ist im Ergebnistext weg.");
    }

    [Test]
    public void CodeActions_IncludeResultTextFalse_OmitsResultTextButKeepsEdits() {

        using var ws   = new McpTestWorkspace();
        var       path = ws.WriteFile("a.nav", UnusedViewNode);

        var result = NavCodeActionsTool.CodeActions(ws.Workspace, path, name: "v", includeResultText: false);

        var remove = result.Actions.Single(a => a.Title == "Remove Unused Nodes");
        Assert.IsNull(remove.ResultText, "Mit includeResultText=false bleibt der Voll-Text weg.");
        CollectionAssert.IsNotEmpty(remove.Edits, "Die Edits bleiben aber erhalten.");
    }

    [Test]
    public void CodeActions_WithoutName_ReturnsActionsFromEveryTaskInTheFile() {

        using var ws   = new McpTestWorkspace();
        var       path = ws.WriteFile("a.nav", TwoTasksEachWithUnusedNode);

        // Ohne Symbolname: alle in der Datei anwendbaren Aktionen. Beide Tasks bieten 'Remove Unused Nodes'
        // an — gleicher Titel, verschiedene Edits. Die Dedup über Titel UND Edit-Signatur behält beide.
        var result = NavCodeActionsTool.CodeActions(ws.Workspace, path);

        Assert.IsNull(result.Error);
        Assert.AreEqual("", result.Name, "Ohne Symbolname bleibt 'Name' leer.");

        var removeActions = result.Actions.Where(a => a.Title == "Remove Unused Nodes").ToList();
        Assert.AreEqual(2, removeActions.Count, "Je Task mit ungenutztem Knoten eine eigene Aktion.");

        // Jede Aktion entfernt genau ihren eigenen Knoten.
        var texts = removeActions.Select(a => a.ResultText).ToList();
        Assert.IsTrue(texts.Any(t => !t!.Contains("view va;") && t.Contains("view vb;")), "Eine Aktion entfernt 'va'.");
        Assert.IsTrue(texts.Any(t => !t!.Contains("view vb;") && t.Contains("view va;")), "Eine Aktion entfernt 'vb'.");
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
