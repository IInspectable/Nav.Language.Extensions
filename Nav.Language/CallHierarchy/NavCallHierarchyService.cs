#region Using Directives

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

#endregion

namespace Pharmatechnik.Nav.Language.CallHierarchy;

/// <summary>
/// VS-freier Engine-Kern für die "Call Hierarchy" (Aufrufliste) auf Task-Ebene — gemeinsam nutzbar von
/// LSP-Server (und perspektivisch VS-Extension), also "eine Engine". Der Aufrufgraph der Nav-Sprache läuft
/// über <see cref="ITaskNodeSymbol"/>: ein TaskNode in einer Task-Definition referenziert (auch cross-file
/// via <c>taskref</c>) die Deklaration einer aufgerufenen Task.
/// <list type="bullet">
/// <item><b>Prepare:</b> verankert die Hierarchie an der Task-Definition, deren Block die Caret-Position
/// enthält (Caret auf einem TaskNode liefert damit dessen enthaltende Task).</item>
/// <item><b>Ausgehend:</b> die von der Task aufgerufenen Tasks (ihre TaskNodes), gruppiert nach Ziel.</item>
/// <item><b>Eingehend:</b> solution-weit alle Tasks, die einen TaskNode auf diese Task enthalten.</item>
/// </list>
/// </summary>
public static class NavCallHierarchyService {

    /// <summary>
    /// Liefert die Task-Definition, an der die Aufrufhierarchie für die angegebene Zeichen-Position
    /// (0-basierter Offset) verankert wird: die Task, deren Definitionsblock die Position enthält — oder
    /// <c>null</c>, wenn die Position in keiner Task-Definition liegt (z.B. auf einer <c>taskref</c>-Zeile).
    /// </summary>
    public static ITaskDefinitionSymbol? PrepareCallHierarchy(CodeGenerationUnit unit, int position) {

        // Tasks sind nicht verschachtelt: höchstens eine Definition umschliesst die Position. Der
        // Definitionsblock (Syntax) deckt Kopf (inkl. Name) und Rumpf ab, sodass der Aufruf von überall
        // innerhalb der Task funktioniert.
        foreach (var task in unit.TaskDefinitions) {
            var extent = task.Syntax.GetLocation();
            if (extent.Start <= position && position <= extent.End) {
                return task;
            }
        }

        return null;
    }

    /// <summary>
    /// Die von <paramref name="task"/> aufgerufenen Tasks: jeder <see cref="ITaskNodeSymbol"/> mit
    /// aufgelöster Deklaration zählt als Aufruf. Nach Ziel-Deklaration gruppiert — mehrere TaskNodes auf
    /// dieselbe Ziel-Task ergeben einen Eintrag mit mehreren Aufrufstellen. Nicht aufgelöste
    /// <c>taskref</c>-Ziele (<see cref="ITaskNodeSymbol.Declaration"/> == null) werden übersprungen.
    /// </summary>
    public static IReadOnlyList<OutgoingCall> GetOutgoingCalls(ITaskDefinitionSymbol task) {

        var result = new List<OutgoingCall>();

        // Der Where-Filter garantiert eine non-null Declaration; die Null sitzt jedoch auf der
        // Declaration-Property (nicht dem Element), sodass die NRT-Flussanalyse hier nicht verengt —
        // daher das begründete `!`.
        var groups = task.NodeDeclarations
                         .OfType<ITaskNodeSymbol>()
                         .Where(tn => tn.Declaration != null)
                         .GroupBy(tn => tn.Declaration!.Location);

        foreach (var group in groups) {
            var target    = group.First().Declaration!;
            var callSites = group.Select(tn => tn.Location).ToList();
            result.Add(new OutgoingCall(target, callSites));
        }

        return result;
    }

    /// <summary>
    /// Solution-weit alle Tasks, die <paramref name="task"/> aufrufen: jeder <see cref="ITaskNodeSymbol"/>
    /// in der gesamten Solution, dessen Deklaration auf diese Task zeigt. Nach aufrufender Task gruppiert —
    /// mehrere Aufrufstellen in derselben Task ergeben einen Eintrag mit mehreren Aufrufstellen.
    /// Der Vergleich läuft über die Deklarations-<see cref="Location"/> (Wert-Gleichheit), exakt wie der
    /// <c>FindReferencesVisitor</c>.
    /// </summary>
    public static async Task<IReadOnlyList<IncomingCall>> GetIncomingCallsAsync(
        ITaskDefinitionSymbol task,
        NavSolution solution,
        CancellationToken cancellationToken) {

        // Identität der gesuchten Task = ihre Deklarations-Location (so referenzieren TaskNodes sie, auch
        // cross-file via taskref). Ohne Deklaration kann es keine Aufrufer geben.
        var targetDeclaration = task.AsTaskDeclaration?.Location;
        if (targetDeclaration == null) {
            return new List<IncomingCall>();
        }

        // ProcessCodeGenerationUnitsAsync scannt parallel (AsParallel) — Treffer thread-safe sammeln.
        var hits = new List<(ITaskDefinitionSymbol caller, Location callSite)>();
        var gate = new object();

        await solution.ProcessCodeGenerationUnitsAsync(
            codeGenerationUnit => {

                foreach (var caller in codeGenerationUnit.TaskDefinitions) {
                    foreach (var taskNode in caller.NodeDeclarations.OfType<ITaskNodeSymbol>()) {

                        if (taskNode.Declaration?.Location == targetDeclaration) {
                            lock (gate) {
                                hits.Add((caller, taskNode.Location));
                            }
                        }
                    }
                }

                return Task.CompletedTask;
            },
            startingUnit: task.CodeGenerationUnit,
            cancellationToken);

        // Nach aufrufender Task gruppieren (Definitions-Location als stabiler Schlüssel).
        var result = new List<IncomingCall>();
        foreach (var group in hits.GroupBy(h => h.caller.Location)) {
            var caller    = group.First().caller;
            var callSites = group.Select(h => h.callSite).ToList();
            result.Add(new IncomingCall(caller, callSites));
        }

        return result;
    }

}

/// <summary>Ein ausgehender Aufruf: die aufgerufene Task-Deklaration und die Aufrufstellen (TaskNodes).</summary>
public sealed class OutgoingCall {

    public OutgoingCall(ITaskDeclarationSymbol target, IReadOnlyList<Location> callSites) {
        Target    = target;
        CallSites = callSites;
    }

    /// <summary>Die aufgerufene Task (Deklaration; kann cross-file/inkludiert sein).</summary>
    public ITaskDeclarationSymbol Target { get; }

    /// <summary>Die Aufrufstellen (TaskNode-Bezeichner) in der aufrufenden Task.</summary>
    public IReadOnlyList<Location> CallSites { get; }

}

/// <summary>Ein eingehender Aufruf: die aufrufende Task und ihre Aufrufstellen (TaskNodes).</summary>
public sealed class IncomingCall {

    public IncomingCall(ITaskDefinitionSymbol caller, IReadOnlyList<Location> callSites) {
        Caller    = caller;
        CallSites = callSites;
    }

    /// <summary>Die aufrufende Task-Definition.</summary>
    public ITaskDefinitionSymbol Caller { get; }

    /// <summary>Die Aufrufstellen (TaskNode-Bezeichner) innerhalb der aufrufenden Task.</summary>
    public IReadOnlyList<Location> CallSites { get; }

}
