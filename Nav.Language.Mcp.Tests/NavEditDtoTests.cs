#region Using Directives

using NUnit.Framework;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.Mcp.Tools;
using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Nav.Language.Mcp.Tests;

/// <summary>
/// Tests der KI-Sicht-DTOs, die die mutierenden/navigierenden Tools nach außen liefern:
/// <see cref="NavEditDto"/> (offset- → 1-basiertes Koordinaten-Mapping samt Skip-Regeln von
/// <see cref="NavEditDto.FromChanges"/>), <see cref="NavLocationDto"/> (1-basiert, <c>IsDeclaration</c>) und
/// <see cref="NavSymbolRef"/> (<c>Task</c> nur bei Knoten). Rein auf den Text-Primitiven bzw. einer aus Text
/// gebauten <see cref="CodeGenerationUnit"/> — kein Workspace/Platte nötig.
/// </summary>
[TestFixture]
public class NavEditDtoTests {

    // Escapte Strings statt Raw-Strings sind hier bewusst gewählt: die exakte Breite der Zeilenumbrüche (LF)
    // ist der Prüfgegenstand des Offset→Zeile/Spalte-Mappings — ein Raw-String brächte CRLF und verschöbe die
    // erwarteten Offsets.
    const string SingleLine = "hello world";
    const string MultiLine  = "aaa\nbbb\nccc";

    [Test]
    public void From_SingleLineChange_MapsToOneBasedColumns() {

        var sourceText = SourceText.From(SingleLine);

        // 'world' liegt bei [6,11) → 1-basiert Spalte 7..12 in Zeile 1.
        var edit = NavEditDto.From(sourceText, TextChange.NewReplace(start: 6, length: 5, text: "there"));

        Assert.AreEqual(1,       edit.Line);
        Assert.AreEqual(7,       edit.Column);
        Assert.AreEqual(1,       edit.EndLine);
        Assert.AreEqual(12,      edit.EndColumn);
        Assert.AreEqual("there", edit.NewText);
    }

    [Test]
    public void From_MultiLineChange_SpansLines() {

        var sourceText = SourceText.From(MultiLine);

        // [2,6) reicht von Zeile 0 (Offset 2) bis Zeile 1 (Offset 6 = Zeilenstart 4 + 2) → 1-basiert Z1S3..Z2S3.
        var edit = NavEditDto.From(sourceText, TextChange.NewReplace(start: 2, length: 4, text: "X"));

        Assert.AreEqual(1, edit.Line);
        Assert.AreEqual(3, edit.Column);
        Assert.AreEqual(2, edit.EndLine);
        Assert.AreEqual(3, edit.EndColumn);
    }

    [Test]
    public void FromChanges_SkipsEmptyChanges() {

        var sourceText = SourceText.From(SingleLine);

        var edits = NavEditDto.FromChanges(sourceText, new[] {
            TextChange.Empty,
            TextChange.NewReplace(start: 0, length: 5, text: "HELLO")
        });

        Assert.AreEqual(1,       edits.Count, "Die leere Änderung wird übersprungen, die echte bleibt.");
        Assert.AreEqual("HELLO", edits[0].NewText);
    }

    [Test]
    public void FromChanges_SkipsChangesBeyondTextEnd() {

        var sourceText = SourceText.From(SingleLine); // Länge 11

        var edits = NavEditDto.FromChanges(sourceText, new[] {
            TextChange.NewReplace(start: 10, length: 5, text: "boom") // Extent-Ende 15 > 11
        });

        CollectionAssert.IsEmpty(edits, "Ein Extent hinter dem Textende wird übersprungen.");
    }

    [Test]
    public void FromChanges_KeepsInsertAtTextEnd() {

        var sourceText = SourceText.From(SingleLine); // Länge 11

        // Ein Insert exakt am Textende (Extent-Ende == Länge) ist gültig und darf NICHT übersprungen werden.
        var edits = NavEditDto.FromChanges(sourceText, new[] {
            TextChange.NewInsert(position: 11, text: "!")
        });

        Assert.AreEqual(1,   edits.Count);
        Assert.AreEqual(1,   edits[0].Line);
        Assert.AreEqual(12,  edits[0].Column, "Einfügeposition am Zeilenende → 1-basiert Spalte 12.");
        Assert.AreEqual("!", edits[0].NewText);
    }

    [Test]
    public void Location_From_IsOneBasedAndCarriesDeclarationFlag() {

        var sourceText = SourceText.From(SingleLine, filePath: @"n:\av\a.nav");
        var location   = sourceText.GetLocation(new TextExtent(start: 6, length: 5));

        var plain = NavLocationDto.From(location);
        Assert.AreEqual(1,  plain.Line);
        Assert.AreEqual(7,  plain.Column);
        Assert.AreEqual(12, plain.EndColumn);
        Assert.IsFalse(plain.IsDeclaration, "Ohne Flag ist IsDeclaration false.");
        StringAssert.EndsWith("a.nav", plain.File.ToLowerInvariant());

        var declaration = NavLocationDto.From(location, isDeclaration: true);
        Assert.IsTrue(declaration.IsDeclaration);
    }

    [Test]
    public void SymbolRef_From_SetsTaskOnlyForNodes() {

        var unit = ParseUnit(
            """
            task A
            {
                init Start;
                exit Done;
                Start --> Done;
            }
            """);

        // Task-Symbol 'A': Task-Scope ist bei einer Task-Definition selbst nicht gesetzt.
        NavNameResolution.Resolve(unit, "A", taskScope: null, kind: null, out var taskSymbol, out _);
        var taskRef = NavSymbolRef.From(taskSymbol!);
        Assert.AreEqual("A",    taskRef.Name);
        Assert.AreEqual("task", taskRef.Kind);
        Assert.IsNull(taskRef.Task, "Für ein Task-Symbol bleibt 'Task' null.");

        // Knoten-Symbol 'Start': trägt seine enthaltende Task ('A') als Scope.
        NavNameResolution.Resolve(unit, "Start", taskScope: null, kind: null, out var nodeSymbol, out _);
        var nodeRef = NavSymbolRef.From(nodeSymbol!);
        Assert.AreEqual("Start", nodeRef.Name);
        Assert.AreEqual("A",     nodeRef.Task, "Für einen Knoten wird die enthaltende Task gesetzt.");
    }

    #region Helpers

    static CodeGenerationUnit ParseUnit(string source) {
        var syntax = Syntax.ParseCodeGenerationUnit(text: source, filePath: @"n:\av\a.nav");
        return CodeGenerationUnit.FromCodeGenerationUnitSyntax(syntax);
    }

    #endregion

}
