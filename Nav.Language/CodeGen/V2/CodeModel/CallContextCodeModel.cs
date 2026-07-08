#region Using Directives

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// Das versionsübergreifende Herzstück des V2-Codegens: die <b>benannte Aufruffläche einer
/// Kanten-Quelle</b> (Init-, Trigger- oder Exit-Transition). Pro tatsächlich vorhandener Nav-Kante
/// trägt der Context genau eine Methode (<c>Show{Node}</c> / <c>Begin{Node}</c> / <c>Exit</c> /
/// <c>End</c>), plus das immer verfügbare <c>Cancel</c>; jede baut ihr Framework-Kommando
/// <b>deferred</b> in einem <c>Func&lt;…&gt;</c>-Thunk und verpackt es im opaken, geschachtelten
/// <c>Result</c>. Der Maschinerie-Rückgabetyp (<see cref="CommandType"/>) ist <c>IINIT_TASK</c> für
/// Init-Transitionen und <c>INavCommand</c> für Trigger/Exit.
/// </summary>
/// <remarks>
/// S5-Gerüst: <b>ohne</b> Continuation (<c>o-^</c>/<c>--^</c>) und <b>ohne</b> Choice-Forward — diese
/// kommen in S6/S7 als weitere Callable-Arten hinzu. Die Reachability faltet Choices heute noch
/// V1-artig auf (relevant erst, wenn Choice-Korpus generiert wird).
/// </remarks>
sealed class CallContextCodeModel {

    /// <summary>Der Name des context-lokalen Backing-Felds auf die tragende <c>{Task}WFSBase</c>.</summary>
    public const string WfsFieldName = "_wfs";

    CallContextCodeModel(string contextTypeName, string commandType, ImmutableList<CallableMethodModel> methods) {
        ContextTypeName = contextTypeName;
        CommandType     = commandType;
        Methods         = methods;
    }

    /// <summary>Der Typname des Contexts (z.B. <c>Init1CallContext</c>, <c>OnRetryCallContext</c>).</summary>
    public string ContextTypeName { get; }

    /// <summary>Der vom <c>Result.Unwrap()</c> gelieferte Framework-Kommandotyp (<c>IINIT_TASK</c>/<c>INavCommand</c>).</summary>
    public string CommandType { get; }

    /// <summary>Die Callable-Methoden des Contexts, in stabiler Reihenfolge (inkl. <c>Cancel</c>).</summary>
    public ImmutableList<CallableMethodModel> Methods { get; }

    /// <summary>
    /// Baut die Aufruffläche aus den erreichbaren Aufrufen einer Quelle. <paramref name="reachableCalls"/>
    /// sind die (noch nicht entdoppelten) <see cref="Call"/>s der Quelle; <paramref name="ownerTaskResult"/>
    /// ist das Ergebnis des <b>umgebenden</b> Tasks (für die fixe <c>Exit</c>-Fabrik, §3.4).
    /// </summary>
    public static CallContextCodeModel Build(string contextTypeName,
                                             string commandType,
                                             IEnumerable<Call> reachableCalls,
                                             ParameterCodeModel ownerTaskResult) {

        // Wie V1: Exits werden im Codegen nicht unterschieden (FoldExits) — mehrere exit-Ziele
        // kollabieren auf eine einzige Exit()-Fabrik.
        var distinct = reachableCalls.Distinct(CallComparer.FoldExits).ToList();

        var entries = new List<Entry>();

        foreach (var call in distinct) {
            switch (call.Node) {
                case IGuiNodeSymbol gui:
                    entries.Add(BuildShowGui(gui, call.EdgeMode.EdgeMode));
                    break;
                case ITaskNodeSymbol task:
                    entries.AddRange(BuildBeginTask(task, call.EdgeMode.EdgeMode));
                    break;
                case IExitNodeSymbol:
                    entries.Add(BuildExit(ownerTaskResult));
                    break;
                case IEndNodeSymbol:
                    entries.Add(BuildEnd());
                    break;
            }
        }

        // Cancel ist immer implizit erreichbar (kein eigener Nav-Ausgang).
        entries.Add(new Entry(SortOrderCancel, "Cancel", new CallableMethodModel("Cancel()", $"{WfsFieldName}.Cancel()")));

        // Stabile Reihenfolge: nach Kategorie (Task/Gui/Exit/End/Cancel), dann Name; OrderBy ist stabil,
        // sodass mehrere Begin{Node}-Überladungen ihre Init-Reihenfolge behalten.
        var methods = entries.OrderBy(e => e.SortOrder)
                             .ThenBy(e => e.Name, StringComparer.Ordinal)
                             .Select(e => e.Method)
                             .ToImmutableList();

        return new CallContextCodeModel(contextTypeName, commandType, methods);
    }

    // -- Callable-Fabriken je Kanten-Art --------------------------------------------------------------

    static Entry BuildShowGui(IGuiNodeSymbol gui, EdgeMode edgeMode) {

        var namePascal = gui.Name.ToPascalcase();
        var toType     = $"{namePascal}{CodeGenInvariants.ToClassNameSuffix}";
        var engine     = GuiEngineMethod(edgeMode);

        return new Entry(
            SortOrderGui,
            $"Show{namePascal}",
            new CallableMethodModel(
                signature: $"Show{namePascal}({toType} to)",
                thunkBody: $"{WfsFieldName}.{engine}(to)"));
    }

    static IEnumerable<Entry> BuildBeginTask(ITaskNodeSymbol task, EdgeMode edgeMode) {

        var declaration = task.Declaration;
        if (declaration == null) {
            yield break;
        }

        var namePascal     = task.Name.ToPascalcase();
        var notImplemented = declaration.CodeNotImplemented;

        var (engine, generic) = TaskEngineMethod(edgeMode);
        var taskResultType    = ParameterCodeModel.TaskResult(declaration).ParameterType;
        var fieldName         = $"{CodeGenFacts.FieldPrefix}{ParameterCodeModel.GetTaskBeginAsParameter(declaration).ParameterName}";
        var afterMethod       = $"{CodeGenFacts.ExitMethodPrefix}{namePascal}";
        var engineCall        = generic ? $"{engine}<{taskResultType}>" : engine;

        // Je Init-Knoten des Ziel-Tasks eine Begin{Node}-Überladung (analog V1-BeginWrapper).
        foreach (var init in declaration.Inits().WhereNotNull()) {

            var parameters = ParameterCodeModel.FromParameterSyntaxes(init.Syntax.CodeParamsDeclaration?.ParameterList)
                                               .ToList();

            var parameterList = String.Join(", ", parameters.Select(p => $"{p.ParameterType} {p.ParameterName}"));
            var argumentList  = String.Join(", ", parameters.Select(p => p.ParameterName));

            string thunkBody;
            if (notImplemented) {
                // [notimplemented]: begin-bar, scheitert aber beim Unwrap()-Aufruf — V1-Timing im Thunk.
                thunkBody = $"throw new NotImplementedException(\"Task {task.Name} is specified as [notimplemented]\")";
            } else {
                thunkBody = $"{WfsFieldName}.{engineCall}(() => {WfsFieldName}.{fieldName}.{CodeGenFacts.BeginMethodPrefix}({argumentList}), {WfsFieldName}.{afterMethod})";
            }

            yield return new Entry(
                SortOrderTask,
                $"Begin{namePascal}",
                new CallableMethodModel(
                    signature: $"Begin{namePascal}({parameterList})",
                    thunkBody: thunkBody));
        }
    }

    static Entry BuildExit(ParameterCodeModel ownerTaskResult) {
        return new Entry(
            SortOrderExit,
            "Exit",
            new CallableMethodModel(
                signature: $"Exit({ownerTaskResult.ParameterType} {ownerTaskResult.ParameterName})",
                thunkBody: $"{WfsFieldName}.InternalTaskResult({ownerTaskResult.ParameterName})"));
    }

    static Entry BuildEnd() {
        return new Entry(
            SortOrderEnd,
            "End",
            new CallableMethodModel(
                signature: "End()",
                thunkBody: $"{WfsFieldName}.EndNonModal()"));
    }

    static string GuiEngineMethod(EdgeMode edgeMode) {
        return edgeMode switch {
            EdgeMode.Modal    => "OpenModalGUI",
            EdgeMode.NonModal => "StartNonModalGUI",
            _                 => "GotoGUI"
        };
    }

    static (string Engine, bool Generic) TaskEngineMethod(EdgeMode edgeMode) {
        return edgeMode switch {
            EdgeMode.Modal    => ("OpenModalTask", true),
            EdgeMode.NonModal => ("StartNonModalTask", false),
            _                 => ("GotoTask", true)
        };
    }

    // Reihenfolge-Kategorien (an V1s CallCodeModel.SortOrder angelehnt): Task, Gui, Exit, End, Cancel.
    const int SortOrderTask   = 1;
    const int SortOrderGui    = 2;
    const int SortOrderExit   = 3;
    const int SortOrderEnd    = 4;
    const int SortOrderCancel = Int32.MaxValue;

    readonly struct Entry {

        public Entry(int sortOrder, string name, CallableMethodModel method) {
            SortOrder = sortOrder;
            Name      = name;
            Method    = method;
        }

        public int                 SortOrder { get; }
        public string              Name      { get; }
        public CallableMethodModel Method    { get; }

    }

}

/// <summary>
/// Eine einzelne Callable-Methode eines <see cref="CallContextCodeModel"/>: ihre Signatur und der
/// Ausdruck, den der deferred <c>Func&lt;…&gt;</c>-Thunk beim <c>Unwrap()</c>-Aufruf auswertet. Der
/// Emitter schreibt daraus uniform <c>public Result {Signature} =&gt; new(() =&gt; {ThunkBody});</c>.
/// </summary>
sealed class CallableMethodModel {

    public CallableMethodModel(string signature, string thunkBody) {
        Signature = signature;
        ThunkBody = thunkBody;
    }

    public string Signature { get; }
    public string ThunkBody { get; }

}
