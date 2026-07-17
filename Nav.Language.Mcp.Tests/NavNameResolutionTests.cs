#region Using Directives

using System.Linq;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.Mcp.Tools;

#endregion

namespace Nav.Language.Mcp.Tests;

/// <summary>
/// Tests der gemeinsamen Namen→Symbol-Auflösung <see cref="NavNameResolution"/> (internal, via
/// <c>InternalsVisibleTo</c>). Verifiziert die drei Ausgänge (Resolved / NotFound / Ambiguous), die beiden
/// Disambiguierungs-Achsen (<c>task</c>-Scope und <c>kind</c>) samt der Leerlauf-Regel des <c>kind</c>-Filters
/// sowie <see cref="NavNameResolution.KindMatches"/>. Arbeitet direkt auf einer aus <c>.nav</c>-Text gebauten
/// <see cref="CodeGenerationUnit"/> — kein Workspace/Platte nötig, da die Auflösung rein auf dem Modell operiert.
/// </summary>
[TestFixture]
public class NavNameResolutionTests {

    // Zwei Tasks, die denselben Knotennamen ('Start') verwenden — Grundlage für Ambiguous und den task-Scope.
    const string TwoTasksSharedNode =
        """
        task A
        {
            init Start;
            exit Done;
            Start --> Done;
        }
        task B
        {
            init Start;
            exit Done;
            Start --> Done;
        }
        """;

    // Eine Task 'Login' mit einem gleichnamigen gui-Knoten ('dialog Login') in eben dieser Task — der Fall, den
    // der task-Scope NICHT auflösen kann (Task + ihr eigener Knoten), sehr wohl aber der kind-Filter.
    const string TaskAndSameNamedGuiNode =
        """
        task Login
        {
            init Start;
            dialog Login;
            exit Done;
            Start --> Login;
            Login --> Done;
        }
        """;

    [Test]
    public void Resolve_UniqueName_IsResolved() {

        var unit = ParseUnit(TwoTasksSharedNode);

        var status = NavNameResolution.Resolve(unit, "A", taskScope: null, kind: null,
                                               out var symbol, out var candidates);

        Assert.AreEqual(NavNameResolution.Status.Resolved, status);
        Assert.IsNotNull(symbol);
        Assert.AreEqual("A", symbol!.Name);
        Assert.AreEqual(1,   candidates.Count);
    }

    [Test]
    public void Resolve_UnknownName_IsNotFound() {

        var unit = ParseUnit(TwoTasksSharedNode);

        var status = NavNameResolution.Resolve(unit, "DoesNotExist", taskScope: null, kind: null,
                                               out var symbol, out var candidates);

        Assert.AreEqual(NavNameResolution.Status.NotFound, status);
        Assert.IsNull(symbol);
        CollectionAssert.IsEmpty(candidates);
    }

    [Test]
    public void Resolve_NodeNameInTwoTasks_IsAmbiguous() {

        var unit = ParseUnit(TwoTasksSharedNode);

        var status = NavNameResolution.Resolve(unit, "Start", taskScope: null, kind: null,
                                               out var symbol, out var candidates);

        Assert.AreEqual(NavNameResolution.Status.Ambiguous, status);
        Assert.IsNull(symbol, "Bei Mehrdeutigkeit bleibt das Ergebnis-Symbol leer.");
        Assert.AreEqual(2, candidates.Count, "Knotenname 'Start' kommt in Task A und B vor.");
    }

    [Test]
    public void Resolve_WithTaskScope_NarrowsToSingleTask() {

        var unit = ParseUnit(TwoTasksSharedNode);

        var status = NavNameResolution.Resolve(unit, "Start", taskScope: "B", kind: null,
                                               out var symbol, out var candidates);

        Assert.AreEqual(NavNameResolution.Status.Resolved, status);
        Assert.AreEqual(1,                                 candidates.Count);
        Assert.AreEqual("B",                               ((INodeSymbol)symbol!).ContainingTask.Name);
    }

    [Test]
    public void Resolve_TaskAndSameNamedNode_DisambiguatedByKind() {

        var unit = ParseUnit(TaskAndSameNamedGuiNode);

        // Ohne kind: Task 'Login' und gui-Knoten 'Login' in derselben Task → mehrdeutig (task-Scope hülfe hier nicht).
        var ambiguous = NavNameResolution.Resolve(unit, "Login", taskScope: null, kind: null,
                                                  out _, out var candidates);
        Assert.AreEqual(NavNameResolution.Status.Ambiguous, ambiguous);
        Assert.AreEqual(2,                                  candidates.Count);

        // kind:"task" wählt die Task-Definition.
        var asTask = NavNameResolution.Resolve(unit, "Login", taskScope: null, kind: "task",
                                               out var taskSymbol, out _);
        Assert.AreEqual(NavNameResolution.Status.Resolved, asTask);
        Assert.IsInstanceOf<ITaskDefinitionSymbol>(taskSymbol);

        // kind:"gui" wählt den Knoten.
        var asGui = NavNameResolution.Resolve(unit, "Login", taskScope: null, kind: "gui",
                                              out var guiSymbol, out _);
        Assert.AreEqual(NavNameResolution.Status.Resolved, asGui);
        Assert.IsInstanceOf<IGuiNodeSymbol>(guiSymbol);
    }

    [Test]
    public void Resolve_KindFilterWithoutMatch_KeepsOriginalCandidates() {

        var unit = ParseUnit(TaskAndSameNamedGuiNode);

        // 'choice' trifft weder die Task noch den gui-Knoten — der Filter läuft ins Leere. Erwartet: die
        // ursprünglichen Kandidaten bleiben erhalten (weiterhin Ambiguous, KEIN falsches NotFound).
        var status = NavNameResolution.Resolve(unit, "Login", taskScope: null, kind: "choice",
                                               out var symbol, out var candidates);

        Assert.AreEqual(NavNameResolution.Status.Ambiguous, status);
        Assert.IsNull(symbol);
        Assert.AreEqual(2, candidates.Count, "Ein leerlaufender kind-Filter darf die Kandidaten nicht wegwerfen.");
    }

    [Test]
    public void KindMatches_NodeIsCollectiveKind() {

        var unit = ParseUnit(TaskAndSameNamedGuiNode);
        var (task, gui) = TaskAndGui(unit);

        // 'node' ist die grobe Sammelart: jeder Knoten passt, die Task nicht.
        Assert.IsTrue(NavNameResolution.KindMatches(gui,   "node"));
        Assert.IsFalse(NavNameResolution.KindMatches(task, "node"));
    }

    [Test]
    public void KindMatches_ConcreteKindIsCaseInsensitive() {

        var unit = ParseUnit(TaskAndSameNamedGuiNode);
        var (task, gui) = TaskAndGui(unit);

        // Konkrete Art exakt, aber case-insensitiv.
        Assert.IsTrue(NavNameResolution.KindMatches(gui,  "gui"));
        Assert.IsTrue(NavNameResolution.KindMatches(gui,  "GUI"));
        Assert.IsTrue(NavNameResolution.KindMatches(task, "task"));
        Assert.IsTrue(NavNameResolution.KindMatches(task, "TASK"));
        Assert.IsFalse(NavNameResolution.KindMatches(gui, "task"));
    }

    #region Helpers

    static CodeGenerationUnit ParseUnit(string source) {
        var syntax = Syntax.ParseCodeGenerationUnit(text: source, filePath: @"n:\av\a.nav");
        return CodeGenerationUnit.FromCodeGenerationUnitSyntax(syntax);
    }

    // Zerlegt die Kandidaten von 'Login' in Task-Definition und gleichnamigen gui-Knoten.
    static (ISymbol task, ISymbol gui) TaskAndGui(CodeGenerationUnit unit) {
        NavNameResolution.Resolve(unit, "Login", taskScope: null, kind: null, out _, out var candidates);
        var task = candidates.OfType<ITaskDefinitionSymbol>().Single();
        var gui  = candidates.OfType<IGuiNodeSymbol>().Single();
        return (task, gui);
    }

    #endregion

}
