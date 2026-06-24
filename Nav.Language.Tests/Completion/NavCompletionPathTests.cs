#region Using Directives

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.Completion;

#endregion

namespace Nav.Language.Tests.Completion;

[TestFixture]
public class NavCompletionPathTests {

    string _dir;
    string _navFile;

    [SetUp]
    public void SetUp() {
        _dir     = Path.Combine(Path.GetTempPath(), "navcompl_" + Guid.NewGuid().ToString("N"));
        _navFile = Path.Combine(_dir, "Main.nav");
    }

    // Baut eine Solution aus den angegebenen (nicht zwingend existierenden) relativen Pfaden.
    NavSolution SolutionWith(params string[] relativePaths) {
        var files = relativePaths.Select(p => new FileInfo(Path.Combine(_dir, p.Replace('/', Path.DirectorySeparatorChar))))
                                 .ToImmutableArray();
        return new NavSolution(new DirectoryInfo(_dir), files);
    }

    const string Nav = "taskref \"\";\n" +
                       "\n"              +
                       "task Main\n"     +
                       "{\n"             +
                       "    init i;\n"   +
                       "    exit e;\n"   +
                       "    i --> e;\n"  +
                       "}\n";

    static int CaretBetweenQuotes(string source) {
        return source.IndexOf("\"\"", StringComparison.Ordinal) + 1; // zwischen den beiden Anführungszeichen
    }

    [Test]
    public void OffersAllSolutionNavFiles_RegardlessOfFolder() {

        var unit     = ParseModel(Nav, _navFile);
        var solution = SolutionWith("Other.nav", "Another.nav", "sub/Deep.nav", "a/b/c/MessageBoxes.nav");

        var items  = NavCompletionService.GetCompletions(unit, CaretBetweenQuotes(Nav), solution);
        var labels = items.Select(i => i.Label).ToArray();

        // Alle Nav-Files werden über ihren Dateinamen angeboten — auch tief verschachtelte.
        Assert.That(labels, Does.Contain("Other.nav"));
        Assert.That(labels, Does.Contain("Another.nav"));
        Assert.That(labels, Does.Contain("Deep.nav"));
        Assert.That(labels, Does.Contain("MessageBoxes.nav"));
        Assert.That(items.All(i => i.Kind == NavCompletionItemKind.File));
    }

    [Test]
    public void DeepFile_InsertsRelativePathAndShowsItAsDetail() {

        var unit     = ParseModel(Nav, _navFile);
        var solution = SolutionWith("a/b/c/MessageBoxes.nav");

        var items = NavCompletionService.GetCompletions(unit, CaretBetweenQuotes(Nav), solution);
        var item  = items.Single(i => i.Label == "MessageBoxes.nav");

        // Anzeige = Dateiname; eingefügt + Detail = relativer Pfad inkl. Unterverzeichnisse.
        Assert.That(item.Kind,              Is.EqualTo(NavCompletionItemKind.File));
        Assert.That(item.InsertText,        Does.Contain("MessageBoxes.nav"));
        Assert.That(item.InsertText,        Does.Contain("a"));
        Assert.That(item.InsertText,        Does.Contain("b"));
        Assert.That(item.Detail,            Is.EqualTo(item.InsertText));
        Assert.That(item.ReplacementExtent, Is.Not.Null);
    }

    [Test]
    public void SameDirectoryFile_InsertsBareFileName() {

        var unit     = ParseModel(Nav, _navFile);
        var solution = SolutionWith("Other.nav");

        var items = NavCompletionService.GetCompletions(unit, CaretBetweenQuotes(Nav), solution);
        var other = items.Single(i => i.Label == "Other.nav");

        Assert.That(other.InsertText, Is.EqualTo("Other.nav")); // gleiches Verzeichnis → nur Dateiname
    }

    [Test]
    public void ExcludesCurrentFileItself() {

        var unit     = ParseModel(Nav, _navFile);
        var solution = SolutionWith("Main.nav", "Other.nav"); // Main.nav ist die gerade editierte Datei

        var items = NavCompletionService.GetCompletions(unit, CaretBetweenQuotes(Nav), solution);

        Assert.That(items.Select(i => i.Label), Does.Not.Contain("Main.nav"));
        Assert.That(items.Select(i => i.Label), Does.Contain("Other.nav"));
    }

    [Test]
    public void CaretInTaskBody_IsNotPathContext() {

        // Außerhalb eines taskref-Strings keine Pfad-Vorschläge.
        var unit     = ParseModel(Nav, _navFile);
        var solution = SolutionWith("Other.nav");
        var caret    = Nav.IndexOf("i --> e;", StringComparison.Ordinal); // im Task-Body, kein String

        var items = NavCompletionService.GetCompletions(unit, caret, solution);

        Assert.That(items.Any(i => i.Kind == NavCompletionItemKind.File), Is.False);
    }

    #region Helpers

    static CodeGenerationUnit ParseModel(string source, string filePath) {
        var syntax = Syntax.ParseCodeGenerationUnit(text: source, filePath: filePath);
        return CodeGenerationUnit.FromCodeGenerationUnitSyntax(syntax);
    }

    #endregion

}
