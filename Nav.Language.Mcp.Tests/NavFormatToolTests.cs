#region Using Directives

using System.Linq;

using NUnit.Framework;

using Nav.Language.Mcp.Tests.Infrastructure;

using Pharmatechnik.Nav.Language.Mcp.Tools;

#endregion

namespace Nav.Language.Mcp.Tests;

/// <summary>
/// Tests des MCP-Tools <c>nav_format</c> (public statische Tool-Methode, direkt aufgerufen). Verifiziert das
/// Voll-Format (kanonischer Default: Tabs, Breite 4), das zeilenbasierte Range-Format (Subset-Garantie), die
/// Idempotenz, die Optionen (<c>includeFormattedText</c>, <c>insertSpaces</c>/<c>tabSize</c>) sowie die
/// Fehlerfälle (ungültiger Zeilenbereich, fehlende Datei). Der Formatter ändert nur den Whitespace zwischen
/// Token, nie den Token-Text — daher wird das read-only-Ergebnis über Idempotenz und Subset geprüft.
/// </summary>
[TestFixture]
public class NavFormatToolTests {

    // Space-eingerückt (4 Leerzeichen) — der kanonische Default ist Tabs, also erzeugt das Voll-Format Edits.
    const string SpaceIndented =
        """
        task A
        {
            init i;
            exit e;
            i --> e;
        }
        """;

    [Test]
    public void Format_FullDocument_ProducesCanonicalTabIndent() {

        using var ws   = new McpTestWorkspace();
        var       path = ws.WriteFile("a.nav", SpaceIndented);

        var result = NavFormatTool.Format(ws.Workspace, path);

        Assert.IsNull(result.Error);
        CollectionAssert.IsNotEmpty(result.Edits, "Die space-eingerückte Datei ist nicht kanonisch → Edits.");
        Assert.IsNotNull(result.FormattedText);
        // Kanonisch wird mit Tabs eingerückt (der Knotenname wird zusätzlich in eine Spalte ausgerichtet).
        StringAssert.Contains("\tinit", result.FormattedText!, "Kanonisch wird mit Tabs eingerückt.");
    }

    [Test]
    public void Format_AlreadyFormatted_IsIdempotent() {

        using var ws   = new McpTestWorkspace();
        var       path = ws.WriteFile("a.nav", SpaceIndented);

        // Einmal formatieren, das Ergebnis zurückschreiben — der zweite Lauf darf nichts mehr ändern.
        var first         = NavFormatTool.Format(ws.Workspace, path);
        var formattedPath = ws.WriteFile("formatted.nav", first.FormattedText!);

        var second = NavFormatTool.Format(ws.Workspace, formattedPath);

        CollectionAssert.IsEmpty(second.Edits, "Eine bereits kanonisch formatierte Datei liefert 0 Edits.");
        Assert.IsNull(second.FormattedText, "Ohne Änderung bleibt formattedText null.");
    }

    [Test]
    public void Format_IncludeFormattedTextFalse_ReturnsEditsOnly() {

        using var ws   = new McpTestWorkspace();
        var       path = ws.WriteFile("a.nav", SpaceIndented);

        var result = NavFormatTool.Format(ws.Workspace, path, includeFormattedText: false);

        CollectionAssert.IsNotEmpty(result.Edits);
        Assert.IsNull(result.FormattedText, "Bei includeFormattedText=false wird der Volltext abbestellt.");
    }

    [Test]
    public void Format_WithSpacesOption_OverridesCanonicalTabs() {

        using var ws   = new McpTestWorkspace();
        var       path = ws.WriteFile("a.nav", SpaceIndented);

        var result = NavFormatTool.Format(ws.Workspace, path, insertSpaces: true, tabSize: 2);

        Assert.IsNotNull(result.FormattedText);
        // Genau 2 Leerzeichen Einzug (ein 4er-Einzug enthielte '\n    init', nicht '\n  init'); Knotenname ausgerichtet.
        StringAssert.Contains("\n  init", result.FormattedText!, "Spaces-Option, Breite 2 → 2-Leerzeichen-Einzug.");
        Assert.IsFalse(result.FormattedText!.Contains("\tinit"), "Kein Tab-Einzug bei der Spaces-Option.");
    }

    [Test]
    public void Format_Range_OnlyTouchesSelectedLines() {

        using var ws = new McpTestWorkspace();

        // Zwei Tasks; nur die Zeilen des zweiten Tasks werden formatiert → die Edits liegen im Bereich.
        var path = ws.WriteFile("a.nav",
                                """
                                task A
                                {
                                    init i;
                                    exit e;
                                    i --> e;
                                }
                                task B
                                {
                                    init i;
                                    exit e;
                                    i --> e;
                                }
                                """);

        // Task B beginnt in Zeile 7; bis Dateiende formatieren.
        var result = NavFormatTool.Format(ws.Workspace, path, startLine: 7, endLine: 12);

        Assert.IsNull(result.Error);
        CollectionAssert.IsNotEmpty(result.Edits);
        Assert.IsTrue(result.Edits.All(e => e.Line >= 7),
                      "Alle Edits liegen im gewählten Zeilenbereich (Task B), Task A bleibt unberührt.");
    }

    [Test]
    public void Format_InvalidLineRange_ReturnsError() {

        using var ws   = new McpTestWorkspace();
        var       path = ws.WriteFile("a.nav", SpaceIndented);

        // startLine hinter dem Dateiende ist ein Fehler (ein zu großes endLine würde dagegen nur geklemmt).
        var result = NavFormatTool.Format(ws.Workspace, path, startLine: 999);

        Assert.IsNotNull(result.Error);
        CollectionAssert.IsEmpty(result.Edits);
    }

    [Test]
    public void Format_MissingFile_ReturnsError() {

        using var ws = new McpTestWorkspace();

        var result = NavFormatTool.Format(ws.Workspace, System.IO.Path.Combine(ws.Root, "nope.nav"));

        Assert.IsNotNull(result.Error);
        CollectionAssert.IsEmpty(result.Edits);
    }

}
