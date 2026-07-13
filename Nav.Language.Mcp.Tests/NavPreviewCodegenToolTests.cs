#region Using Directives

using System.Linq;

using NUnit.Framework;

using Nav.Language.Mcp.Tests.Infrastructure;

using Pharmatechnik.Nav.Language.Mcp.Tools;

#endregion

namespace Nav.Language.Mcp.Tests;

/// <summary>
/// Tests des MCP-Tools <c>nav_preview_codegen</c> (public statische Tool-Methode, direkt aufgerufen).
/// Verifiziert die in-memory generierten C#-Artefakte je Task (Rollen, Dateinamen, Inhalt), den
/// <c>task</c>-Filter, das Ein-/Ausblenden der Benutzer-Stubs, den reinen Manifest-Modus
/// (<c>includeContent=false</c>) sowie den Fehlerpfad (Codegen verweigert bei Fehler-Diagnostics). Gegen
/// einen Temp-Workspace mit hingeschriebenen <c>.nav</c>-Fixtures.
/// </summary>
[TestFixture]
public class NavPreviewCodegenToolTests {

    const string LoginTask =
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
    public void Preview_ValidTask_ReturnsGeneratedArtifacts() {

        using var ws   = new McpTestWorkspace();
        var       path = ws.WriteFile("a.nav", LoginTask);

        var result = NavPreviewCodegenTool.PreviewCodegen(ws.Workspace, path);

        Assert.IsNull(result.Error);
        Assert.AreEqual(1, result.LanguageVersion, "Ohne #version gilt Generation 1.");
        Assert.AreEqual(1, result.Tasks.Count);

        var task = result.Tasks[0];
        Assert.AreEqual("Login", task.Task);

        // Ohne includeUserFiles: Interfaces + abstrakte Basisklasse, kein Benutzer-Stub.
        var roles = task.Artifacts.Select(a => a.Role).ToList();
        CollectionAssert.Contains(roles, "base");
        CollectionAssert.Contains(roles, "iwfs");
        CollectionAssert.Contains(roles, "ibegin");
        CollectionAssert.DoesNotContain(roles, "user");

        // Die Basisklasse trägt die (abstrakte) Logik und ist der wertvollste Teil.
        var baseArtifact = task.Artifacts.Single(a => a.Role == "base");
        Assert.AreEqual("LoginWFSBase.generated.cs", baseArtifact.FileName);
        Assert.AreEqual("WhenChanged", baseArtifact.OverwritePolicy);
        StringAssert.Contains("abstract", baseArtifact.Content);
        StringAssert.Contains("LoginWFSBase", baseArtifact.Content);
        Assert.Greater(baseArtifact.LineCount, 0);
        Assert.AreEqual(baseArtifact.Content!.Length, baseArtifact.CharCount);
    }

    [Test]
    public void Preview_Version2File_DispatchesToV2Codegen() {

        using var ws = new McpTestWorkspace();
        var path = ws.WriteFile("a.nav",
                                """
                                #version 2
                                task Flow
                                    [result bool]
                                {
                                    init Start;
                                    view Home;
                                    exit Done;

                                    Start --> Home;
                                    Home  --> Done on OnClose;
                                }
                                """);

        var result = NavPreviewCodegenTool.PreviewCodegen(ws.Workspace, path);

        Assert.IsNull(result.Error);
        Assert.AreEqual(2, result.LanguageVersion, "#version 2 → Generation 2.");

        // Die Weiche (VersionDispatchingCodeGenerator) muss CodeGeneratorV2 gewählt haben: die V2-Basis-
        // klasse trägt die verschachtelten CallContext-Typen samt .Unwrap() — Marker, die V1 nie erzeugt.
        var baseArtifact = result.Tasks.Single().Artifacts.Single(a => a.Role == "base");
        StringAssert.Contains("CallContext", baseArtifact.Content, "V2-Codegen erzeugt CallContext-Typen.");
        StringAssert.Contains("Unwrap",      baseArtifact.Content, "V2-Codegen kapselt das Ergebnis über Unwrap().");
    }

    [Test]
    public void Preview_IncludeUserFiles_AddsTheOneShotStub() {

        using var ws   = new McpTestWorkspace();
        var       path = ws.WriteFile("a.nav", LoginTask);

        var result = NavPreviewCodegenTool.PreviewCodegen(ws.Workspace, path, includeUserFiles: true);

        Assert.IsNull(result.Error);

        var userArtifact = result.Tasks.Single().Artifacts.Single(a => a.Role == "user");
        Assert.AreEqual("LoginWFS.cs", userArtifact.FileName);
        Assert.AreEqual("Never", userArtifact.OverwritePolicy, "Der Benutzer-Stub wird nie überschrieben.");
    }

    [Test]
    public void Preview_TaskFilter_NarrowsToOneTask() {

        using var ws = new McpTestWorkspace();
        var path = ws.WriteFile("a.nav",
                                """
                                task First
                                {
                                    init i;
                                    exit e;
                                    i --> e;
                                }

                                task Second
                                {
                                    init i;
                                    exit e;
                                    i --> e;
                                }
                                """);

        var all = NavPreviewCodegenTool.PreviewCodegen(ws.Workspace, path);
        Assert.AreEqual(2, all.Tasks.Count, "Ohne Filter alle Tasks der Datei.");

        var filtered = NavPreviewCodegenTool.PreviewCodegen(ws.Workspace, path, task: "Second");
        Assert.IsNull(filtered.Error);
        Assert.AreEqual(1, filtered.Tasks.Count);
        Assert.AreEqual("Second", filtered.Tasks[0].Task);
    }

    [Test]
    public void Preview_UnknownTask_ReturnsError() {

        using var ws   = new McpTestWorkspace();
        var       path = ws.WriteFile("a.nav", LoginTask);

        var result = NavPreviewCodegenTool.PreviewCodegen(ws.Workspace, path, task: "Nope");

        Assert.IsNotNull(result.Error);
        CollectionAssert.IsEmpty(result.Tasks);
    }

    [Test]
    public void Preview_ManifestOnly_OmitsContentButKeepsCounts() {

        using var ws   = new McpTestWorkspace();
        var       path = ws.WriteFile("a.nav", LoginTask);

        var result = NavPreviewCodegenTool.PreviewCodegen(ws.Workspace, path, includeContent: false);

        Assert.IsNull(result.Error);
        Assert.IsFalse(result.ContentOmitted, "Bewusst abbestellt, nicht wegen Budget weggelassen.");

        foreach (var artifact in result.Tasks.SelectMany(t => t.Artifacts)) {
            Assert.IsNull(artifact.Content, "Inhalt abbestellt.");
            Assert.Greater(artifact.LineCount, 0, "Das Manifest trägt weiterhin die Zeilenzahl.");
            Assert.Greater(artifact.CharCount, 0, "Das Manifest trägt weiterhin die Zeichenzahl.");
        }
    }

    [Test]
    public void Preview_FileWithErrors_RefusesAndReturnsDiagnostics() {

        using var ws = new McpTestWorkspace();
        var path = ws.WriteFile("a.nav",
                                """
                                task A
                                {
                                    init i;
                                    exit e;
                                    i --> missing;
                                }
                                """);

        var result = NavPreviewCodegenTool.PreviewCodegen(ws.Workspace, path);

        Assert.IsNotNull(result.Error, "Codegen verweigert bei Fehler-Diagnostics.");
        CollectionAssert.IsEmpty(result.Tasks);
        Assert.IsNotEmpty(result.Diagnostics);
        Assert.IsTrue(result.Diagnostics.All(d => d.Severity == "Error"),
                      "Nur Fehler-Diagnostics werden gemeldet (sie blockieren den Codegen).");
    }

    [Test]
    public void Preview_MissingFile_ReturnsNotFound() {

        using var ws = new McpTestWorkspace();

        var result = NavPreviewCodegenTool.PreviewCodegen(ws.Workspace, System.IO.Path.Combine(ws.Root, "nope.nav"));

        Assert.IsNotNull(result.Error);
        CollectionAssert.IsEmpty(result.Tasks);
    }

}
