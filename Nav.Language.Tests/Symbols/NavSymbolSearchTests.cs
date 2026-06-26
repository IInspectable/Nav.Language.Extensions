#region Using Directives

using System;
using System.Linq;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.Symbols;

#endregion

namespace Nav.Language.Tests.Symbols;

[TestFixture]
public class NavSymbolSearchTests {

    // Zwei Tasks, die denselben Knotennamen (I1 / done) verwenden — Grundlage für die Mehrdeutigkeits-Tests.
    const string TwoTasks = "task A\n"           +
                            "{\n"                +
                            "    init I1;\n"     +
                            "    exit done;\n"   +
                            "    I1 --> done;\n" +
                            "}\n"                +
                            "task B\n"           +
                            "{\n"                +
                            "    init I1;\n"     +
                            "    exit done;\n"   +
                            "    I1 --> done;\n" +
                            "}\n";

    [Test]
    public void FindByName_TaskDefinition_ReturnsSingleSymbolAtName() {

        var unit = ParseModel(TwoTasks);

        var results = NavSymbolSearch.FindByName(unit, "A");

        var expected = IndexOfToken(TwoTasks, "task A", "task ");
        Assert.That(results.Count,             Is.EqualTo(1));
        Assert.That(results[0].Name,           Is.EqualTo("A"));
        Assert.That(results[0].Location.Start, Is.EqualTo(expected));
    }

    [Test]
    public void FindByName_NodeNameInTwoTasks_IsAmbiguous() {

        var unit = ParseModel(TwoTasks);

        var results = NavSymbolSearch.FindByName(unit, "I1");

        Assert.That(results.Count,                      Is.EqualTo(2), "Knotenname I1 kommt in Task A und B vor.");
        Assert.That(results.All(r => r is INodeSymbol), Is.True);
        Assert.That(results.Select(r => ((INodeSymbol)r).ContainingTask.Name),
                    Is.EquivalentTo(new[] { "A", "B" }));
    }

    [Test]
    public void FindByName_WithTaskScope_NarrowsToThatTask() {

        var unit = ParseModel(TwoTasks);

        var results = NavSymbolSearch.FindByName(unit, "I1", taskScope: "B");

        Assert.That(results.Count,                                 Is.EqualTo(1));
        Assert.That(((INodeSymbol)results[0]).ContainingTask.Name, Is.EqualTo("B"));

        // Der Treffer liegt im Block von Task B (nach dessen 'task B').
        var taskBStart = TwoTasks.IndexOf("task B", StringComparison.Ordinal);
        Assert.That(results[0].Location.Start, Is.GreaterThan(taskBStart));
    }

    [Test]
    public void FindByName_UnknownName_ReturnsEmpty() {

        var unit = ParseModel(TwoTasks);

        Assert.That(NavSymbolSearch.FindByName(unit, "DoesNotExist"), Is.Empty);
    }

    [Test]
    public void FindByName_NullOrEmptyName_ReturnsEmpty() {

        var unit = ParseModel(TwoTasks);

        Assert.That(NavSymbolSearch.FindByName(unit, null), Is.Empty);
        Assert.That(NavSymbolSearch.FindByName(unit, ""),   Is.Empty);
    }

    [Test]
    public void FindDefinitionsByPrefix_TaskNamePrefix_MatchesTaskDefinition() {

        var unit = ParseModel(TwoTasks);

        var results = NavSymbolSearch.FindDefinitionsByPrefix(unit, "A");

        Assert.That(results.Count,   Is.EqualTo(1));
        Assert.That(results[0].Name, Is.EqualTo("A"));
        Assert.That(results[0],      Is.InstanceOf<ITaskDefinitionSymbol>());
    }

    [Test]
    public void FindDefinitionsByPrefix_IsCaseInsensitive() {

        var unit = ParseModel(TwoTasks);

        // Knotenname I1 ist in Task A und B definiert — kleingeschriebener Präfix muss beide finden.
        var results = NavSymbolSearch.FindDefinitionsByPrefix(unit, "i1");

        Assert.That(results.Count,                      Is.EqualTo(2));
        Assert.That(results.All(r => r is INodeSymbol), Is.True);
        Assert.That(results.Select(r => ((INodeSymbol)r).ContainingTask.Name),
                    Is.EquivalentTo(new[] { "A", "B" }));
    }

    [Test]
    public void FindDefinitionsByPrefix_EmptyPrefix_MatchesAllDefinitions() {

        var unit = ParseModel(TwoTasks);

        var results = NavSymbolSearch.FindDefinitionsByPrefix(unit, "");

        // 2 Task-Definitionen (A, B) + je init I1 und exit done = 6 Definitionen.
        Assert.That(results.Count, Is.EqualTo(6));
    }

    [Test]
    public void FindDefinitionsByPrefix_UnknownPrefix_ReturnsEmpty() {

        var unit = ParseModel(TwoTasks);

        Assert.That(NavSymbolSearch.FindDefinitionsByPrefix(unit, "DoesNotExist"), Is.Empty);
    }

    #region Helpers

    static int IndexOfToken(string source, string anchor, string leading) {
        var anchorIndex = source.IndexOf(anchor, StringComparison.Ordinal);
        Assert.That(anchorIndex, Is.GreaterThanOrEqualTo(0), $"Anker '{anchor}' nicht gefunden.");
        return anchorIndex + leading.Length;
    }

    static CodeGenerationUnit ParseModel(string source) {
        var syntax = Syntax.ParseCodeGenerationUnit(text: source, filePath: @"n:\av\a.nav");
        return CodeGenerationUnit.FromCodeGenerationUnitSyntax(syntax);
    }

    #endregion

}