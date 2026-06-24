#region Using Directives

using System;
using System.Linq;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.Completion;

#endregion

namespace Nav.Language.Tests.Completion;

[TestFixture]
public class NavCompletionServiceTests {

    const string Nav = "taskref Sub\n"          +
                       "{\n"                     +
                       "    init si;\n"          +
                       "    exit se;\n"          +
                       "}\n"                     +
                       "\n"                      +
                       "task Main\n"             +
                       "{\n"                     +
                       "    init i;\n"           +
                       "    exit e;\n"           +
                       "    task Sub;\n"         +
                       "    i      --> Sub;\n"   +
                       "    Sub:se --> e;\n"     +
                       "}\n";

    [Test]
    public void AfterTaskKeyword_OffersTaskDeclarations() {

        var unit  = ParseModel(Nav, @"n:\av\a.nav");
        var caret = IndexOfToken(Nav, "task Sub;", "task "); // direkt hinter `task ` vor dem Knotennamen

        var items = NavCompletionService.GetCompletions(unit, caret);

        Assert.That(Labels(items), Does.Contain("Sub"));
        Assert.That(items.Single(i => i.Label == "Sub").Kind, Is.EqualTo(NavCompletionItemKind.Task));
        // Reine Task-Vervollständigung — keine Keywords gemischt.
        Assert.That(Labels(items), Has.None.EqualTo("init"));
    }

    [Test]
    public void AfterNodeColon_OffersExitConnectionPoints() {

        var unit  = ParseModel(Nav, @"n:\av\a.nav");
        var caret = IndexOfToken(Nav, "Sub:se --> e;", "Sub:"); // direkt hinter `Sub:`

        var items = NavCompletionService.GetCompletions(unit, caret);

        Assert.That(Labels(items), Does.Contain("se"));
        Assert.That(items.Single(i => i.Label == "se").Kind, Is.EqualTo(NavCompletionItemKind.ConnectionPoint));
    }

    [Test]
    public void InTaskBody_OffersNodesAndKeywords() {

        var unit  = ParseModel(Nav, @"n:\av\a.nav");
        var caret = IndexOfToken(Nav, "i      --> Sub;", "i      --> "); // auf der Knotenreferenz 'Sub'

        var items  = NavCompletionService.GetCompletions(unit, caret);
        var labels = Labels(items);

        // Knoten der Task-Definition...
        Assert.That(labels, Does.Contain("i"));
        Assert.That(labels, Does.Contain("e"));
        Assert.That(labels, Does.Contain("Sub"));
        // ...und Nav-Keywords (aber keine Edge-Keywords).
        Assert.That(labels, Does.Contain("exit"));
        Assert.That(items.Where(x => x.Kind == NavCompletionItemKind.Keyword).Select(x => x.Label),
                    Has.None.EqualTo("-->"));
    }

    [Test]
    public void InSingleLineComment_OffersNothing() {

        const string nav = "task A\n"          +
                           "{\n"               +
                           "    // hier nix\n" +
                           "    init i;\n"     +
                           "    exit e;\n"     +
                           "    i --> e;\n"    +
                           "}\n";

        var unit  = ParseModel(nav, @"n:\av\c.nav");
        var caret = IndexOfToken(nav, "// hier nix", "// h"); // mitten im Kommentar

        Assert.That(NavCompletionService.GetCompletions(unit, caret), Is.Empty);
    }

    [Test]
    public void InStringLiteral_OffersNothing() {

        const string nav = "taskref \"Sub.nav\";\n" +
                           "\n"                      +
                           "task A\n"                +
                           "{\n"                     +
                           "    init i;\n"           +
                           "    exit e;\n"           +
                           "    i --> e;\n"          +
                           "}\n";

        var unit  = ParseModel(nav, @"n:\av\s.nav");
        var caret = IndexOfToken(nav, "\"Sub.nav\"", "\"S"); // innerhalb der Zeichenkette

        Assert.That(NavCompletionService.GetCompletions(unit, caret), Is.Empty);
    }

    [Test]
    public void InCodeBlock_OffersNothing() {

        const string nav = "[using Foo]\n" +
                           "\n"            +
                           "task A\n"      +
                           "{\n"           +
                           "    init i;\n" +
                           "    exit e;\n" +
                           "    i --> e;\n" +
                           "}\n";

        var unit  = ParseModel(nav, @"n:\av\b.nav");
        var caret = IndexOfToken(nav, "[using Foo]", "[using F"); // innerhalb des Code-Blocks

        Assert.That(NavCompletionService.GetCompletions(unit, caret), Is.Empty);
    }

    #region Helpers

    static string[] Labels(System.Collections.Generic.IReadOnlyList<NavCompletionItem> items) {
        return items.Select(i => i.Label).ToArray();
    }

    static int IndexOfToken(string source, string anchor, string leading) {
        var anchorIndex = source.IndexOf(anchor, StringComparison.Ordinal);
        Assert.That(anchorIndex, Is.GreaterThanOrEqualTo(0), $"Anker '{anchor}' nicht gefunden.");
        return anchorIndex + leading.Length;
    }

    static CodeGenerationUnit ParseModel(string source, string filePath) {
        var syntax = Syntax.ParseCodeGenerationUnit(text: source, filePath: filePath);
        return CodeGenerationUnit.FromCodeGenerationUnitSyntax(syntax);
    }

    #endregion

}
