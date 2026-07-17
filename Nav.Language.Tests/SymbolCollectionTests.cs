#region Using Directives

using NUnit.Framework;

using Pharmatechnik.Nav.Language;

#endregion

namespace Nav.Language.Tests;

[TestFixture]
public class SymbolCollectionTests {

    const string Nav = @"
task A
{
    init I1;
    exit e1;

    I1  --> e1;
}
";

    [Test]
    public void TryFindSymbolFindsExistingSymbol() {

        var taskA = ParseTaskA();

        var init = taskA.NodeDeclarations.TryFindSymbol("I1");

        Assert.That(init,      Is.Not.Null);
        Assert.That(init.Name, Is.EqualTo("I1"));
    }

    [Test]
    public void TryFindSymbolReturnsNullForUnknownKey() {

        var taskA = ParseTaskA();

        Assert.That(taskA.NodeDeclarations.TryFindSymbol("gibtsNicht"), Is.Null);
    }

    [Test]
    public void TryFindSymbolReturnsNullForNullOrEmptyKey() {

        var taskA = ParseTaskA();

        Assert.That(taskA.NodeDeclarations.TryFindSymbol(null), Is.Null);
        Assert.That(taskA.NodeDeclarations.TryFindSymbol(""),   Is.Null);
    }

    [Test]
    public void TryFindSymbolOnEmptyCollectionReturnsNull() {

        // Leere Collection: die KeyedCollection legt ihr Lookup-Dictionary erst beim ersten Add an
        var model = ParseModel("");

        Assert.That(model.TaskDeclarations,                    Is.Empty);
        Assert.That(model.TaskDeclarations.TryFindSymbol("A"), Is.Null);
    }

    ITaskDefinitionSymbol ParseTaskA() {

        var model = ParseModel(Nav);
        var taskA = model.TryFindTaskDefinition("A");

        Assert.That(taskA, Is.Not.Null);

        return taskA;
    }

    static CodeGenerationUnit ParseModel(string source) {
        var syntax = Syntax.ParseCodeGenerationUnit(source);
        return CodeGenerationUnit.FromCodeGenerationUnitSyntax(syntax);
    }

}
