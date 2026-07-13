#region Using Directives

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.CallHierarchy;

#endregion

namespace Nav.Language.Tests.CallHierarchy;

[TestFixture]
public class NavCallHierarchyServiceTests {

    // task A ruft task B auf (Knoten 'b' vom Typ B); B ist in derselben Datei definiert.
    //   |a:A|      Bezeichner der Task-Definition 'A'
    //   |bNode:B|  TaskNode 'B b' im Rumpf von A (aufgerufener Typ)
    static readonly NavMarkup M = NavMarkup.Parse(
        """
        task |a:A|
        {
            init i;
            task |bNode:B| b;
            exit e;
            i    --> b;
            b:x  --> e;
        }
        task B
        {
            init i;
            exit x;
            i --> x;
        }

        """);

    #region Prepare

    [Test]
    public void Prepare_OnTaskDefinitionName_ReturnsThatTask() {

        var unit  = ParseModel(M.Source, @"n:\av\a.nav");
        var caret = M.Position("a"); // Bezeichner 'A'

        var task = NavCallHierarchyService.PrepareCallHierarchy(unit, caret);

        Assert.That(task, Is.Not.Null);
        Assert.That(task!.Name, Is.EqualTo("A"));
    }

    [Test]
    public void Prepare_InsideTaskBody_ReturnsContainingTask() {

        var unit  = ParseModel(M.Source, @"n:\av\a.nav");
        var caret = M.Position("bNode"); // TaskNode 'B b' im Rumpf von A

        var task = NavCallHierarchyService.PrepareCallHierarchy(unit, caret);

        Assert.That(task, Is.Not.Null);
        Assert.That(task!.Name, Is.EqualTo("A")); // enthaltende Task, nicht die aufgerufene
    }

    [Test]
    public void Prepare_OutsideAnyTask_ReturnsNull() {

        var m = NavMarkup.Parse(
            """
            |taskref "lib.nav";

            task A
            {
                init i;
                exit e;
                i --> e;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\a.nav");
        var caret = m.Caret; // ausserhalb jeder Task-Definition

        var task = NavCallHierarchyService.PrepareCallHierarchy(unit, caret);

        Assert.That(task, Is.Null);
    }

    #endregion

    #region Outgoing

    [Test]
    public void Outgoing_ReturnsCalledTask() {

        var unit = ParseModel(M.Source, @"n:\av\a.nav");
        var a    = unit.TaskDefinitions.Single(t => t.Name == "A");

        var calls = NavCallHierarchyService.GetOutgoingCalls(a);

        Assert.That(calls, Has.Count.EqualTo(1));
        Assert.That(calls[0].Target.Name, Is.EqualTo("B"));
        Assert.That(calls[0].CallSites,   Has.Count.EqualTo(1));
    }

    [Test]
    public void Outgoing_MultipleCallSitesSameTarget_AreGrouped() {

        const string src = "task A\n"          +
                           "{\n"               +
                           "    init i;\n"     +
                           "    task B b1;\n"  +
                           "    task B b2;\n"  +
                           "    exit e;\n"     +
                           "    i     --> b1;\n" +
                           "    b1:x  --> b2;\n" +
                           "    b2:x  --> e;\n"  +
                           "}\n"               +
                           "task B\n"          +
                           "{\n"               +
                           "    init i;\n"     +
                           "    exit x;\n"     +
                           "    i --> x;\n"    +
                           "}\n";

        var unit = ParseModel(src, @"n:\av\a.nav");
        var a    = unit.TaskDefinitions.Single(t => t.Name == "A");

        var calls = NavCallHierarchyService.GetOutgoingCalls(a);

        Assert.That(calls, Has.Count.EqualTo(1));          // ein Ziel B ...
        Assert.That(calls[0].Target.Name, Is.EqualTo("B"));
        Assert.That(calls[0].CallSites,   Has.Count.EqualTo(2)); // ... mit zwei Aufrufstellen
    }

    [Test]
    public void Outgoing_UnresolvedTaskRef_IsSkipped() {

        const string src = "task A\n"           +
                           "{\n"                +
                           "    init i;\n"      +
                           "    task Ghost g;\n" + // 'Ghost' nirgends definiert
                           "    exit e;\n"      +
                           "    i --> g;\n"     +
                           "}\n";

        var unit = ParseModel(src, @"n:\av\a.nav");
        var a    = unit.TaskDefinitions.Single(t => t.Name == "A");

        var calls = NavCallHierarchyService.GetOutgoingCalls(a);

        Assert.That(calls, Is.Empty); // keine aufgelöste Deklaration → kein ausgehender Aufruf
    }

    #endregion

    #region Incoming (solution-weit, echte Dateien)

    [Test]
    public async Task Incoming_SameFile_ReturnsCaller() {

        using var tmp = new TempSolution();
        tmp.Write("a.nav", M.Source);

        var (solution, unit) = await tmp.LoadAsync("a.nav");
        var b = unit.TaskDefinitions.Single(t => t.Name == "B");

        var calls = await NavCallHierarchyService.GetIncomingCallsAsync(b, solution, CancellationToken.None);

        Assert.That(calls,             Has.Count.EqualTo(1));
        Assert.That(calls[0].Caller.Name, Is.EqualTo("A"));
        Assert.That(calls[0].CallSites,   Has.Count.EqualTo(1));
        Assert.That(calls[0].CallSites[0].FilePath, Is.EqualTo(tmp.Path("a.nav")));
    }

    [Test]
    public async Task Incoming_CrossFile_ReturnsCallerFromOtherFile() {

        const string main = "taskref \"lib.nav\";\n" +
                            "task M\n"               +
                            "{\n"                    +
                            "    init i;\n"          +
                            "    task Sub s;\n"      +
                            "    exit e;\n"          +
                            "    i    --> s;\n"      +
                            "    s:x  --> e;\n"      +
                            "}\n";

        const string lib = "task Sub\n"    +
                           "{\n"           +
                           "    init i;\n" +
                           "    exit x;\n" +
                           "    i --> x;\n" +
                           "}\n";

        using var tmp = new TempSolution();
        tmp.Write("main.nav", main);
        tmp.Write("lib.nav", lib);

        var (solution, libUnit) = await tmp.LoadAsync("lib.nav");
        var sub = libUnit.TaskDefinitions.Single(t => t.Name == "Sub");

        var calls = await NavCallHierarchyService.GetIncomingCallsAsync(sub, solution, CancellationToken.None);

        Assert.That(calls,                Has.Count.EqualTo(1));
        Assert.That(calls[0].Caller.Name, Is.EqualTo("M"));
        Assert.That(calls[0].CallSites[0].FilePath, Is.EqualTo(tmp.Path("main.nav")));
    }

    [Test]
    public async Task Incoming_SelfRecursion_ListsTaskItself() {

        const string src = "task R\n"         +
                           "{\n"              +
                           "    init i;\n"    +
                           "    task R r;\n"  + // R ruft sich selbst auf
                           "    exit e;\n"    +
                           "    i    --> r;\n" +
                           "    r:x  --> e;\n" +
                           "}\n";

        using var tmp = new TempSolution();
        tmp.Write("r.nav", src);

        var (solution, unit) = await tmp.LoadAsync("r.nav");
        var r = unit.TaskDefinitions.Single(t => t.Name == "R");

        var calls = await NavCallHierarchyService.GetIncomingCallsAsync(r, solution, CancellationToken.None);

        Assert.That(calls,                Has.Count.EqualTo(1));
        Assert.That(calls[0].Caller.Name, Is.EqualTo("R"));
    }

    [Test]
    public async Task Incoming_StartingUnitPathCasingDiffers_DoesNotDoubleCount() {

        // Regression: NavSolution.ProcessCodeGenerationUnitsAsync deduplizierte die durchlaufenen Dateien
        // früher case-sensitiv (HashSet<string>). Weicht die Schreibweise des startingUnit-Pfads von der
        // der SolutionFiles ab — z.B. wenn ein Host (MCP) pfad-normalisierte, kleingeschriebene Pfade
        // hereinreicht —, greift die Dedup nicht und die aufrufende Datei wird DOPPELT verarbeitet:
        // derselbe Aufruf erscheint zweimal. Hier deterministisch erzwungen über eine Ausgangs-Unit, deren
        // (realer) Pfad großgeschrieben ist, während die Solution aus der Original-Schreibweise geladen wird.

        const string main =
            """
            taskref "lib.nav";
            task M
            {
                init i;
                task Sub s;
                exit e;
                i    --> s;
                s:x  --> e;
            }
            """;

        const string lib =
            """
            task Sub
            {
                init i;
                exit x;
                i --> x;
            }
            """;

        using var tmp = new TempSolution();
        tmp.Write("main.nav", main);
        tmp.Write("lib.nav",  lib);

        // Solution aus der Original-Schreibweise laden (deren SolutionFiles tragen diese Schreibweise).
        var (solution, _) = await tmp.LoadAsync("lib.nav");

        // Ausgangs-Unit bewusst über einen abweichend geschriebenen, aber auf dieselbe Datei zeigenden Pfad.
        var recasedLibPath = tmp.Path("lib.nav").ToUpperInvariant();
        var sub            = ParseModel(lib, recasedLibPath).TaskDefinitions.Single(t => t.Name == "Sub");

        var calls = await NavCallHierarchyService.GetIncomingCallsAsync(sub, solution, CancellationToken.None);

        Assert.That(calls,                Has.Count.EqualTo(1), "genau ein Aufrufer (M)");
        Assert.That(calls[0].Caller.Name, Is.EqualTo("M"));
        Assert.That(calls[0].CallSites,   Has.Count.EqualTo(1),
                    "case-insensitive Datei-Dedup: main.nav darf trotz abweichender Pfad-Schreibweise nicht doppelt gezählt werden");
    }

    [Test]
    public async Task Incoming_NoCallers_ReturnsEmpty() {

        using var tmp = new TempSolution();
        tmp.Write("a.nav", M.Source);

        var (solution, unit) = await tmp.LoadAsync("a.nav");
        var a = unit.TaskDefinitions.Single(t => t.Name == "A"); // A wird von niemandem aufgerufen

        var calls = await NavCallHierarchyService.GetIncomingCallsAsync(a, solution, CancellationToken.None);

        Assert.That(calls, Is.Empty);
    }

    #endregion

    #region Helpers

    static CodeGenerationUnit ParseModel(string source, string filePath) {
        var syntax = Syntax.ParseCodeGenerationUnit(text: source, filePath: filePath);
        return CodeGenerationUnit.FromCodeGenerationUnitSyntax(syntax);
    }

    /// <summary>
    /// Schreibt echte .nav-Dateien in ein Temp-Verzeichnis und baut daraus eine <see cref="NavSolution"/>.
    /// Nötig, weil die solution-weite Incoming-Suche über <c>ProcessCodeGenerationUnitsAsync</c> das
    /// Dateisystem iteriert (Verzeichnis-Enumeration + Lesen je Datei).
    /// </summary>
    sealed class TempSolution: IDisposable {

        readonly string _dir;

        public TempSolution() {
            _dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "navch_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public string Path(string fileName) => System.IO.Path.Combine(_dir, fileName);

        public void Write(string fileName, string content) => File.WriteAllText(Path(fileName), content);

        public async Task<(NavSolution solution, CodeGenerationUnit unit)> LoadAsync(string fileName) {
            var solution = await NavSolution.FromDirectoryAsync(new DirectoryInfo(_dir), CancellationToken.None);
            var unit     = solution.SemanticModelProvider.GetSemanticModel(Path(fileName), CancellationToken.None);
            return (solution, unit);
        }

        public void Dispose() {
            try {
                Directory.Delete(_dir, recursive: true);
            } catch {
                // Best effort — Temp-Aufräumen darf den Testlauf nicht zum Scheitern bringen.
            }
        }
    }

    #endregion

}
