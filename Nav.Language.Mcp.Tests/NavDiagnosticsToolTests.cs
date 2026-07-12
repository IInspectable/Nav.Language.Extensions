#region Using Directives

using System.Linq;
using System.Threading.Tasks;

using NUnit.Framework;

using Nav.Language.Mcp.Tests.Infrastructure;

using Pharmatechnik.Nav.Language.Mcp.Tools;

#endregion

namespace Nav.Language.Mcp.Tests;

/// <summary>
/// Tests des MCP-Tools <c>nav_diagnostics</c> — das workspace-weite Gegenstück zu <c>nav_validate</c>
/// (public statische Tool-Methode, direkt aufgerufen). Verifiziert die Summary-Konsistenz (Zählungen vor
/// Paging), den <c>severity</c>-Filter samt Normalisierungs-Kanten, den pfadbasierten <c>filter</c>, das
/// Paging über die Diagnostics (nicht die Dateien) sowie den Quer-Check gegen <c>nav_validate</c>. Gegen
/// einen Temp-Workspace mit hingeschriebenen <c>.nav</c>-Fixtures bekannter Fehlerlage — der natürliche
/// Fixture-Mechanismus des MCP, der rein gegen Platte arbeitet.
/// </summary>
[TestFixture]
public class NavDiagnosticsToolTests {

    // Genau ein Error (Nav0010): der Task-Knoten 'C' verweist auf eine nicht auflösbare Task 'C' und wird
    // per 'I1 --> C' auch benutzt — sonst gäbe es zusätzlich eine "nicht benötigt"-Warnung.
    const string BrokenTask =
        """
        task A
        {
            init I1;
            exit e1;
            task C;

            I1 --> e1;
            I1 --> C;
        }
        """;

    // Genau eine Warnung (Nav1005): die taskref-Deklaration 'A' wird nirgends benötigt.
    const string UnusedTaskref =
        """
        taskref A
        {
            init i;
            exit e;
        }

        task B
        {
            init i;
            exit e;
            i --> e;
        }
        """;

    // Komplett sauber — keine Diagnostics.
    const string CleanTask =
        """
        task A
        {
            init i;
            exit e;
            i --> e;
        }
        """;

    // Drei Dateien in getrennten Unterordnern (für den filter-Scope): 'alpha' < 'beta' < 'clean' in
    // OrdinalIgnoreCase — legt zugleich die stabile Diagnostics-Reihenfolge fest (Error vor Warning).
    static McpTestWorkspace CreateWorkspace() {
        var ws = new McpTestWorkspace();
        ws.WriteFile("alpha/broken.nav", BrokenTask);
        ws.WriteFile("beta/unused.nav",  UnusedTaskref);
        ws.WriteFile("clean.nav",        CleanTask);
        return ws;
    }

    [Test]
    public async Task Diagnostics_Summary_IsConsistentAndCountedBeforePaging() {

        using var ws = CreateWorkspace();

        var result = await NavDiagnosticsTool.Diagnostics(ws.Workspace);

        // Alle drei Dateien werden gescannt (parsebar); zwei davon tragen Diagnostics.
        Assert.AreEqual(3, result.FilesScanned);
        Assert.AreEqual(2, result.FilesWithDiagnostics);

        // Je genau ein Error und eine Warnung, keine Suggestion.
        Assert.AreEqual(1, result.Summary.Error);
        Assert.AreEqual(1, result.Summary.Warning);
        Assert.AreEqual(0, result.Summary.Suggestion);

        // Summary summiert sich zur Gesamtzahl (beide vor Paging).
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual(result.Summary.Error + result.Summary.Warning + result.Summary.Suggestion, result.Count);

        Assert.AreEqual(2, result.Returned);
        Assert.IsFalse(result.Truncated);
    }

    [Test]
    public async Task Diagnostics_StableOrder_ErrorBeforeWarning() {

        using var ws = CreateWorkspace();

        var result = await NavDiagnosticsTool.Diagnostics(ws.Workspace);

        // Sortiert nach relativem Pfad (OrdinalIgnoreCase): alpha/broken.nav vor beta/unused.nav.
        Assert.AreEqual("Error",   result.Diagnostics[0].Severity);
        Assert.AreEqual("Warning", result.Diagnostics[1].Severity);
        StringAssert.EndsWith("broken.nav", result.Diagnostics[0].Path.ToLowerInvariant());
        StringAssert.EndsWith("unused.nav", result.Diagnostics[1].Path.ToLowerInvariant());
    }

    [Test]
    public async Task Diagnostics_SeverityFilter_NarrowsToSelectedSeverity() {

        using var ws = CreateWorkspace();

        var errors = await NavDiagnosticsTool.Diagnostics(ws.Workspace, severity: "error");
        Assert.AreEqual(1, errors.Count);
        Assert.AreEqual(1, errors.FilesWithDiagnostics, "Nur die Datei mit dem Error zählt jetzt.");
        Assert.IsTrue(errors.Diagnostics.All(d => d.Severity == "Error"));

        var warnings = await NavDiagnosticsTool.Diagnostics(ws.Workspace, severity: "warning");
        Assert.AreEqual(1, warnings.Count);
        Assert.IsTrue(warnings.Diagnostics.All(d => d.Severity == "Warning"));
    }

    [Test]
    public async Task Diagnostics_SeverityFilter_IsCaseInsensitive() {

        using var ws = CreateWorkspace();

        var result = await NavDiagnosticsTool.Diagnostics(ws.Workspace, severity: "ERROR");

        Assert.AreEqual(1, result.Count);
        Assert.IsTrue(result.Diagnostics.All(d => d.Severity == "Error"));
    }

    [Test]
    public async Task Diagnostics_UnknownSeverity_IsNoFilter() {

        using var ws = CreateWorkspace();

        // Dokumentiertes Verhalten: eine unbekannte severity-Eingabe filtert NICHT (kein falsches "nichts
        // gefunden") — alle Diagnostics bleiben sichtbar.
        var result = await NavDiagnosticsTool.Diagnostics(ws.Workspace, severity: "bogus");

        Assert.AreEqual(2, result.Count);
    }

    [Test]
    public async Task Diagnostics_WhitespaceSeverity_IsNoFilter() {

        using var ws = CreateWorkspace();

        var result = await NavDiagnosticsTool.Diagnostics(ws.Workspace, severity: "   ");

        Assert.AreEqual(2, result.Count, "Whitespace-severity ist wie null: alle Diagnostics.");
    }

    [Test]
    public async Task Diagnostics_PathFilter_ScopesToSubfolderCaseInsensitive() {

        using var ws = CreateWorkspace();

        // filter ist ein Substring auf dem relativen Pfad, case-insensitiv — hier auf den 'alpha'-Ordner.
        var result = await NavDiagnosticsTool.Diagnostics(ws.Workspace, filter: "ALPHA");

        Assert.AreEqual(1, result.FilesScanned, "Nur die Datei im alpha-Ordner wird gescannt.");
        Assert.AreEqual(1, result.Count);
        Assert.IsTrue(result.Diagnostics.All(d => d.Severity == "Error"));
    }

    [Test]
    public async Task Diagnostics_PathFilter_NoMatch_ScansNothing() {

        using var ws = CreateWorkspace();

        var result = await NavDiagnosticsTool.Diagnostics(ws.Workspace, filter: "does-not-exist");

        Assert.AreEqual(0, result.FilesScanned);
        Assert.AreEqual(0, result.Count);
        CollectionAssert.IsEmpty(result.Diagnostics);
    }

    [Test]
    public async Task Diagnostics_Paging_IsOverDiagnosticsNotFiles() {

        using var ws = CreateWorkspace();

        // Zwei Diagnostics gesamt: erste Seite (limit 1) liefert den Error, es folgt noch die Warnung.
        var firstPage = await NavDiagnosticsTool.Diagnostics(ws.Workspace, limit: 1, offset: 0);
        Assert.AreEqual(2, firstPage.Count, "count zählt VOR dem Paging alle Diagnostics.");
        Assert.AreEqual(1, firstPage.Returned);
        Assert.AreEqual("Error", firstPage.Diagnostics[0].Severity);
        Assert.IsTrue(firstPage.Truncated);

        // Letzte Seite: die verbleibende Warnung, danach folgt nichts mehr.
        var lastPage = await NavDiagnosticsTool.Diagnostics(ws.Workspace, limit: 1, offset: 1);
        Assert.AreEqual(1, lastPage.Returned);
        Assert.AreEqual(1, lastPage.Offset);
        Assert.AreEqual("Warning", lastPage.Diagnostics[0].Severity);
        Assert.IsFalse(lastPage.Truncated);
    }

    [Test]
    public async Task Diagnostics_LimitAboveMax_IsClampedTo200() {

        using var ws = CreateWorkspace();

        var result = await NavDiagnosticsTool.Diagnostics(ws.Workspace, limit: 500);

        Assert.AreEqual(200, result.Limit);
    }

    [Test]
    public async Task Diagnostics_NonPositiveLimit_FallsBackToDefault() {

        using var ws = CreateWorkspace();

        var zero     = await NavDiagnosticsTool.Diagnostics(ws.Workspace, limit: 0);
        var negative = await NavDiagnosticsTool.Diagnostics(ws.Workspace, limit: -5);

        Assert.AreEqual(100, zero.Limit);
        Assert.AreEqual(100, negative.Limit);
    }

    [Test]
    public async Task Diagnostics_NegativeOffset_IsClampedToZero() {

        using var ws = CreateWorkspace();

        var result = await NavDiagnosticsTool.Diagnostics(ws.Workspace, offset: -3);

        Assert.AreEqual(0, result.Offset);
    }

    [Test]
    public async Task Diagnostics_FilteredToFile_MatchesNavValidate() {

        using var ws = CreateWorkspace();

        var brokenPath = ws.WriteFile("alpha/broken.nav", BrokenTask);

        // Quer-Check (das bisherige manuelle Smoke-Szenario, jetzt automatisiert): nav_validate einer
        // Einzeldatei muss identische Codes/Counts liefern wie das auf diese Datei gefilterte nav_diagnostics.
        var validate    = NavValidateTool.Validate(ws.Workspace, brokenPath);
        var diagnostics = await NavDiagnosticsTool.Diagnostics(ws.Workspace, filter: "broken.nav");

        Assert.AreEqual(validate.ErrorCount + validate.WarningCount + validate.SuggestionCount, diagnostics.Count);

        var validateCodes    = validate.Diagnostics.Select(d => d.Code).OrderBy(c => c);
        var diagnosticsCodes = diagnostics.Diagnostics.Select(d => d.Code).OrderBy(c => c);
        CollectionAssert.AreEqual(validateCodes, diagnosticsCodes);
    }

}
