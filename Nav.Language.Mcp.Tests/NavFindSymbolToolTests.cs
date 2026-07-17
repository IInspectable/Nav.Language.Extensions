#region Using Directives

using System.Linq;
using System.Threading.Tasks;

using NUnit.Framework;

using Nav.Language.Mcp.Tests.Infrastructure;

using Pharmatechnik.Nav.Language.Mcp.Tools;

#endregion

namespace Nav.Language.Mcp.Tests;

/// <summary>
/// Tests des MCP-Tools <c>nav_find_symbol</c> (public statische Tool-Methode, direkt aufgerufen). Verifiziert
/// die solution-weite Präfix-Suche (case-insensitiv, cross-file, nur Definitionen), den <c>kind</c>-Filter, die
/// stabile Sortierung samt Dedup sowie die Paging-Kanten (Clamping, Default, Truncated-Grenze). Gegen einen
/// Temp-Workspace mit hingeschriebenen <c>.nav</c>-Fixtures — der natürliche Fixture-Mechanismus des MCP, der
/// rein gegen Platte arbeitet.
/// </summary>
[TestFixture]
public class NavFindSymbolToolTests {

    // Zwei Tasks in zwei Dateien, die dieselben Knotennamen ('Start'/'Done') tragen — deckt cross-file,
    // Case-Insensitivität, Sortierung und Dedup ab. 'a.nav' sortiert vor 'sub/b.nav'.
    const string TaskA =
        """
        task Alpha
        {
            init Start;
            exit Done;
            Start --> Done;
        }
        """;

    const string TaskB =
        """
        task Beta
        {
            init Start;
            exit Done;
            Start --> Done;
        }
        """;

    // Eine reine taskref-Deklaration (Import) — liefert KEINE Definition und darf daher nie als Treffer erscheinen.
    const string TaskRefOnly =
        """
        taskref Imported
        {
            init In;
            exit Out;
        }
        """;

    static McpTestWorkspace CreateWorkspace() {
        var ws = new McpTestWorkspace();
        ws.WriteFile("a.nav",           TaskA);
        ws.WriteFile("sub/b.nav",       TaskB);
        ws.WriteFile("declaration.nav", TaskRefOnly);
        return ws;
    }

    [Test]
    public async Task FindSymbol_EmptyPrefix_ReturnsAllDefinitions() {

        using var ws = CreateWorkspace();

        var result = await NavFindSymbolTool.FindSymbol(ws.Workspace, prefix: "");

        // 2 Task-Definitionen (Alpha, Beta) + je init Start und exit Done = 6 Definitionen; taskref zählt nicht.
        Assert.AreEqual(6, result.MatchCount);
        Assert.AreEqual(6, result.Returned);
    }

    [Test]
    public async Task FindSymbol_Prefix_IsCaseInsensitiveCrossFileAndSorted() {

        using var ws = CreateWorkspace();

        // 'Start' kommt in beiden Dateien vor; kleingeschrieben, um die Case-Insensitivität mitzuprüfen.
        var result = await NavFindSymbolTool.FindSymbol(ws.Workspace, prefix: "start");

        Assert.AreEqual(2, result.MatchCount, "'Start' ist je einmal in a.nav und sub/b.nav definiert.");
        Assert.IsTrue(result.Symbols.All(s => s.Name == "Start"));

        // Stabile Sortierung nach Pfad: a.nav vor sub/b.nav. Dedup zeigt sich daran, dass jeder Treffer nur einmal steht.
        StringAssert.EndsWith("a.nav", result.Symbols[0].File.ToLowerInvariant());
        StringAssert.EndsWith("b.nav", result.Symbols[1].File.ToLowerInvariant());
    }

    [Test]
    public async Task FindSymbol_UnknownPrefix_ReturnsEmpty() {

        using var ws = CreateWorkspace();

        var result = await NavFindSymbolTool.FindSymbol(ws.Workspace, prefix: "DoesNotExist");

        Assert.AreEqual(0, result.MatchCount);
        CollectionAssert.IsEmpty(result.Symbols);
    }

    [Test]
    public async Task FindSymbol_TaskrefDeclaration_IsNotADefinition() {

        using var ws = CreateWorkspace();

        // 'Imported' ist nur als taskref deklariert — nav_find_symbol liefert ausschließlich Definitionen.
        var result = await NavFindSymbolTool.FindSymbol(ws.Workspace, prefix: "Imported");

        Assert.AreEqual(0, result.MatchCount, "taskref-Deklarationen sind keine Definitionen.");
    }

    [Test]
    public async Task FindSymbol_KindFilter_NarrowsBySymbolKind() {

        using var ws = CreateWorkspace();

        var tasks = await NavFindSymbolTool.FindSymbol(ws.Workspace, prefix: "", kind: "task");
        Assert.AreEqual(2, tasks.MatchCount, "Genau die beiden Task-Definitionen.");
        Assert.IsTrue(tasks.Symbols.All(s => s.Kind == "task"));

        var nodes = await NavFindSymbolTool.FindSymbol(ws.Workspace, prefix: "", kind: "node");
        Assert.AreEqual(4, nodes.MatchCount, "'node' ist die Sammelart: alle vier Knoten (2× init, 2× exit).");

        var inits = await NavFindSymbolTool.FindSymbol(ws.Workspace, prefix: "", kind: "init");
        Assert.AreEqual(2, inits.MatchCount, "Genau die beiden init-Knoten.");
        Assert.IsTrue(inits.Symbols.All(s => s.Kind == "init"));
    }

    [Test]
    public async Task FindSymbol_LimitAboveMax_IsClampedTo200() {

        using var ws = CreateWorkspace();

        var result = await NavFindSymbolTool.FindSymbol(ws.Workspace, prefix: "", limit: 500);

        Assert.AreEqual(200, result.Limit, "limit wird auf das Maximum (200) gedeckelt.");
    }

    [Test]
    public async Task FindSymbol_NonPositiveLimit_FallsBackToDefault() {

        using var ws = CreateWorkspace();

        var zero     = await NavFindSymbolTool.FindSymbol(ws.Workspace, prefix: "", limit: 0);
        var negative = await NavFindSymbolTool.FindSymbol(ws.Workspace, prefix: "", limit: -5);

        Assert.AreEqual(100, zero.Limit, "limit <= 0 fällt auf den Default (100) zurück.");
        Assert.AreEqual(100, negative.Limit);
    }

    [Test]
    public async Task FindSymbol_NegativeOffset_IsClampedToZero() {

        using var ws = CreateWorkspace();

        var result = await NavFindSymbolTool.FindSymbol(ws.Workspace, prefix: "", offset: -3);

        Assert.AreEqual(0, result.Offset,   "Ein negativer offset wird auf 0 geklemmt.");
        Assert.AreEqual(6, result.Returned, "Alle Definitionen kommen ab offset 0.");
    }

    [Test]
    public async Task FindSymbol_Paging_TruncatedWhenMorePagesRemain() {

        using var ws = CreateWorkspace();

        // Erste Seite von 6 Treffern: 4 zurück, es folgen weitere → truncated.
        var firstPage = await NavFindSymbolTool.FindSymbol(ws.Workspace, prefix: "", limit: 4, offset: 0);
        Assert.AreEqual(6, firstPage.MatchCount);
        Assert.AreEqual(4, firstPage.Returned);
        Assert.AreEqual(0, firstPage.Offset);
        Assert.IsTrue(firstPage.Truncated, "Nach 4 von 6 Treffern folgen noch weitere.");

        // Letzte Seite: die restlichen 2, nichts folgt mehr.
        var lastPage = await NavFindSymbolTool.FindSymbol(ws.Workspace, prefix: "", limit: 4, offset: 4);
        Assert.AreEqual(2, lastPage.Returned);
        Assert.AreEqual(4, lastPage.Offset);
        Assert.IsFalse(lastPage.Truncated);
    }

    [Test]
    public async Task FindSymbol_ExactlyFullPage_IsNotTruncated() {

        using var ws = CreateWorkspace();

        // Genau volle Seite (limit == Gesamtzahl) ist die dokumentierte Kante: NICHT truncated.
        var result = await NavFindSymbolTool.FindSymbol(ws.Workspace, prefix: "", limit: 6, offset: 0);

        Assert.AreEqual(6, result.Returned);
        Assert.IsFalse(result.Truncated, "Eine exakt volle Seite ist nicht truncated.");
    }

}
