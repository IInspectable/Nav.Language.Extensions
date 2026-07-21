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
/// <c>End</c>), plus <c>Cancel</c> genau dann, wenn die Quelle einen <c>cancel</c>-Ausgang deklariert
/// (<c>… --&gt; cancel …</c>, V2-Gating; siehe <see cref="Build"/>); jede baut ihr Framework-Kommando
/// <b>deferred</b> in einem <c>Func&lt;…&gt;</c>-Thunk und verpackt es im opaken, geschachtelten
/// <c>Result</c>. Der Maschinerie-Rückgabetyp (<see cref="CommandType"/>) ist <c>IINIT_TASK</c> für
/// Init-Transitionen und <c>INavCommand</c> für Trigger/Exit.
/// </summary>
/// <remarks>
/// Trägt eine View-Kante eine Continuation (<c>… o-^ Task</c> / <c>… --^ Task</c>), liefert die
/// <c>Show{Node}</c>-Methode keinen <c>Result</c> mehr, sondern einen geschachtelten
/// <c>Show{Node}Continuation</c>-Typ mit je einer <c>Begin{Task}(…)</c>-Fortsetzung (baut
/// <c>GotoGUI(to).Concat(…)</c>) und — sofern eine plain-Schwesterkante zur selben View existiert —
/// einem impliziten <c>Result</c>-Operator (§3.4/§3.6). Der Choice-Forward folgt in S7.
/// </remarks>
sealed class CallContextCodeModel {

    /// <summary>
    /// Der Name des Rückverweises auf die tragende <c>{Task}WFSBase</c>: im <c>{Context}CallContext</c> der
    /// Primärkonstruktor-Parameter (vom Compiler als privates Feld eingefangen), im geschachtelten
    /// Continuation-Typ ein explizites <c>readonly</c>-Feld (dessen statischer Result-Operator es qualifiziert
    /// über eine Fremdinstanz liest).
    /// </summary>
    public const string WfsFieldName = "_wfs";

    /// <summary>Der Name des <c>ViewTO</c>-Backing-Felds im geschachtelten Continuation-Typ.</summary>
    public const string ToFieldName = "_to";

    /// <summary>Der Parametername der <c>Show{Node}</c>-Methode (und des Continuation-Konstruktors).</summary>
    public const string ToParameterName = "to";

    /// <summary>
    /// Der Parametername des Call-Context-Parameters in der <c>…Logic(…)</c>-Signatur — der Empfänger,
    /// über den der handgeschriebene Logic-Body navigiert (<c>next.Show{Node}(…)</c>, <c>next.Begin{Task}(…)</c>,
    /// <c>next.Exit(…)</c> …). Bewusst emitter-intern (nicht in <see cref="ICodeGenFacts"/>): eine andere
    /// Generation bringt ihr eigenes Vokabular mit.
    /// </summary>
    public const string ContextParameterName = "next";

    CallContextCodeModel(string contextTypeName, string commandType, string logicMethodName, ImmutableList<CallableModel> methods) {
        ContextTypeName = contextTypeName;
        CommandType     = commandType;
        LogicMethodName = logicMethodName;
        Methods         = methods;
    }

    /// <summary>Der Typname des Contexts (z.B. <c>Init1CallContext</c>, <c>OnRetryCallContext</c>).</summary>
    public string ContextTypeName { get; }

    /// <summary>Der vom <c>Result.Unwrap()</c> gelieferte Framework-Kommandotyp (<c>IINIT_TASK</c>/<c>INavCommand</c>).</summary>
    public string CommandType { get; }

    /// <summary>
    /// Der Name der Logic-Methode, deren Override diesen Context bekommt (z.B. <c>BeginLogic</c>,
    /// <c>Choice_RetryLogic</c>). <c>Result.Unwrap()</c> reicht ihn als <c>nameof(…)</c> an den zentralen
    /// Guard durch, damit dessen Meldung das schuldige Override benennt — beim Wurf ist die Logic bereits
    /// returned und steht <b>nicht</b> mehr auf dem Stack (der Stacktrace zeigt nur die Maschinerie- bzw.
    /// beim Choice-Forward einen Compiler-generierten Lambda-Frame).
    /// </summary>
    public string LogicMethodName { get; }

    /// <summary>Die Callables des Contexts, in stabiler Reihenfolge (inkl. <c>Cancel</c>).</summary>
    public ImmutableList<CallableModel> Methods { get; }

    /// <summary>
    /// Baut die Aufruffläche aus den <b>direkten</b> Aufrufen einer Quelle. <paramref name="directCalls"/>
    /// sind die (noch nicht entdoppelten) <see cref="Call"/>s der Quelle — <b>ohne</b> plattgefaltete
    /// Choices (<see cref="EdgeExtensions.GetDirectCalls"/>): ein Choice-Ziel ist ein eigener
    /// <see cref="Call"/> und wird zu einem <c>{Choice}(…)</c>-Forward (§3.5). <paramref name="ownerTaskResult"/>
    /// ist das Ergebnis des <b>umgebenden</b> Tasks (für die fixe <c>Exit</c>-Factory, §3.4).
    /// </summary>
    /// <param name="declaresCancel">
    /// Ob die Quelle einen <c>cancel</c>-Ausgang deklariert (<c>… --&gt; cancel …</c>, erkannt via
    /// <see cref="EdgeExtensions.TargetsCancel"/>). Nur dann bekommt der Context die <c>Cancel()</c>-Callable.
    /// Fehlt die Deklaration, fehlt die Callable — ein <c>return next.Cancel()</c> in der Logik ist dann ein
    /// <b>Compile-Fehler</b> (die geerbte Framework-<c>Cancel()</c> liefert <c>CANCEL</c>, nicht den opaken
    /// Context-<c>Result</c>; E3). So können Deklaration und Implementierung in V2 nicht auseinanderlaufen.
    /// </param>
    public static CallContextCodeModel Build(string contextTypeName,
                                             string commandType,
                                             string logicMethodName,
                                             IEnumerable<Call> directCalls,
                                             ParameterCodeModel ownerTaskResult,
                                             bool declaresCancel) {

        // Wie V1: Exits werden im Codegen nicht unterschieden (FoldExits) — mehrere exit-Ziele
        // kollabieren auf eine einzige Exit()-Factory.
        var distinct = directCalls.Distinct(CallComparer.FoldExits).ToList();

        var entries = new List<Entry>();

        // GUI-Kanten werden pro Ziel-Knoten gebündelt (§3.4-Union): eine Quelle mit plain- UND
        // Continuation-Kante zur selben View bekommt EINE Show{Node}-Methode.
        var guiGroups = distinct.Where(call => call.Node is IGuiNodeSymbol)
                                .GroupBy(call => call.Node.Name, StringComparer.Ordinal);

        foreach (var guiGroup in guiGroups) {
            entries.Add(BuildShowGui(guiGroup.ToList()));
        }

        foreach (var call in distinct) {
            switch (call.Node) {
                case IGuiNodeSymbol:
                    // bereits über guiGroups behandelt
                    break;
                case ITaskNodeSymbol task:
                    entries.AddRange(BuildBeginTask(task, call.EdgeMode.EdgeMode));
                    break;
                case IChoiceNodeSymbol choice:
                    entries.Add(BuildChoiceForward(choice));
                    break;
                case IExitNodeSymbol:
                    entries.Add(BuildExit(ownerTaskResult));
                    break;
                case IEndNodeSymbol:
                    entries.Add(BuildEnd());
                    break;
            }
        }

        // V2-Gating (E3): die Cancel()-Callable nur, wenn die Quelle einen cancel-Ausgang deklariert.
        // Ohne Deklaration fehlt sie → ein return next.Cancel() in der Logik ist ein Compile-Fehler.
        if (declaresCancel) {
            entries.Add(new Entry(SortOrderCancel, "Cancel", new CallableMethodModel("Cancel()", $"{WfsFieldName}.Cancel()")));
        }

        // Stabile Reihenfolge: nach Kategorie (Task/Gui/Exit/End/Cancel), dann Name; OrderBy ist stabil,
        // sodass mehrere Begin{Node}-Überladungen ihre Init-Reihenfolge behalten.
        var methods = entries.OrderBy(e => e.SortOrder)
                             .ThenBy(e => e.Name, StringComparer.Ordinal)
                             .Select(e => e.Method)
                             .ToImmutableList();

        return new CallContextCodeModel(contextTypeName, commandType, logicMethodName, methods);
    }

    // -- Callable-Factorys je Kanten-Art --------------------------------------------------------------

    /// <summary>
    /// Baut die <c>Show{Node}</c>-Aufruffläche für alle Kanten einer Quelle zur selben GUI-View
    /// (<paramref name="calls"/>). Trägt keine Kante eine Continuation, ist es die schlichte
    /// <c>Show{Node}(ViewTO) =&gt; Result</c>-Factory (Grundform); trägt mindestens eine Kante eine
    /// Continuation, entsteht der <c>Show{Node}Continuation</c>-Typ (§3.4/§3.6).
    /// </summary>
    static Entry BuildShowGui(IReadOnlyList<Call> calls) {

        var gui        = (IGuiNodeSymbol) calls[0].Node;
        var namePascal = gui.Name.ToPascalcase();
        var toType     = $"{namePascal}{CodeGenInvariants.ToClassNameSuffix}";
        var showName   = $"Show{namePascal}";

        var plainCalls        = calls.Where(call => call.ContinuationCall == null).ToList();
        var continuationCalls = calls.Where(call => call.ContinuationCall != null).ToList();

        if (continuationCalls.Count == 0) {
            // Grundform: mode-freie Show-Factory. Der Anzeige-Modus steckt in der Engine-Methode.
            var engine = GuiEngineMethod(plainCalls[0].EdgeMode.EdgeMode);
            return new Entry(
                SortOrderGui,
                showName,
                new CallableMethodModel(
                    signature: $"{showName}({toType} {ToParameterName})",
                    thunkBody: $"{WfsFieldName}.{engine}({ToParameterName})"));
        }

        // Continuation: Show{Node} liefert den Continuation-Typ. Ein plain-Operator wird nur emittiert,
        // wenn zusätzlich eine plain-Schwesterkante existiert (§3.6 — sonst „erzwungene Continuation").
        string? plainThunk = null;
        if (plainCalls.Count > 0) {
            var plainEngine = GuiEngineMethod(plainCalls[0].EdgeMode.EdgeMode);
            plainThunk = $"v.{WfsFieldName}.{plainEngine}(v.{ToFieldName})";
        }

        var begins = new List<CallableMethodModel>();
        foreach (var call in continuationCalls) {

            var carrierEngine  = GuiEngineMethod(call.EdgeMode.EdgeMode);
            var continuation   = call.ContinuationCall!;
            var continuationTask = (ITaskNodeSymbol) continuation.Node;

            foreach (var piece in BuildTaskBegins(continuationTask, continuation.EdgeMode.EdgeMode)) {

                // [notimplemented]: der Concat-Boundary wäre ein throw-Ausdruck, der nicht als Argument
                // stehen kann → der ganze Thunk ist dann der throw (V1-Timing, feuert beim Unwrap()).
                var thunkBody = piece.NotImplemented
                    ? piece.BoundaryExpression
                    : $"{WfsFieldName}.{carrierEngine}({ToFieldName}).Concat({piece.BoundaryExpression})";

                begins.Add(new CallableMethodModel($"{piece.Name}({piece.ParameterList})", thunkBody, piece.InterfaceFqn));
            }
        }

        return new Entry(
            SortOrderGui,
            showName,
            new ShowContinuationCallableModel(
                entryMethodSignature: $"{showName}({toType} {ToParameterName})",
                continuationTypeName: $"{showName}{ContinuationTypeSuffix}",
                toParameterType     : toType,
                plainThunkBody      : plainThunk,
                begins              : begins.ToImmutableList()));
    }

    static IEnumerable<Entry> BuildBeginTask(ITaskNodeSymbol task, EdgeMode edgeMode) {
        foreach (var piece in BuildTaskBegins(task, edgeMode)) {
            yield return new Entry(
                SortOrderTask,
                piece.Name,
                new CallableMethodModel(
                    signature           : $"{piece.Name}({piece.ParameterList})",
                    thunkBody           : piece.BoundaryExpression,
                    navInitCallInterface: piece.InterfaceFqn));
        }
    }

    /// <summary>
    /// Berechnet je Init-Knoten des Ziel-Tasks die Bausteine einer <c>Begin{Node}</c>-Fortsetzung: den
    /// Methodennamen, die Parameterliste und den <b>Boundary-Ausdruck</b> — das Framework-Kommando
    /// <c>{engine}(() =&gt; _wfs._x.Begin(args), _wfs.After{Node})</c> (bzw. den <c>throw</c> bei
    /// <c>[notimplemented]</c>). Der Ausdruck ist wortgleich als eigenständiger <c>Begin{Node}</c>-Thunk
    /// (Task-Kante) wie als <c>.Concat(…)</c>-Argument (Continuation) verwendbar — beide Kontexte tragen
    /// ein <c>_wfs</c>-Feld.
    /// </summary>
    /// <remarks>
    /// <c>[donotinject]</c> (§3.4): der Ziel-Task-Wrapper wird <b>nicht</b> als <c>_wfs._x</c>-Feld
    /// injiziert (das Semantic Model kennt eine Familie konkreter, laufzeit-selektierter
    /// Implementierungen). Statt über das fehlende Feld nimmt <c>Begin{Node}</c> den Wrapper als
    /// <b>expliziten ersten Parameter</b> (<c>IBegin{Task}WFS wfs</c>) entgegen und ruft <c>wfs.Begin(…)</c>
    /// — der originalgetreue V1-Port (dort <c>BeginDoSomething(IBegin…WFS wfs)</c>).
    /// </remarks>
    static IEnumerable<TaskBeginPiece> BuildTaskBegins(ITaskNodeSymbol task, EdgeMode edgeMode) {

        var declaration = task.Declaration;
        if (declaration == null) {
            yield break;
        }

        var namePascal     = task.Name.ToPascalcase();
        var notImplemented = declaration.CodeNotImplemented;
        var doNotInject    = task.CodeDoNotInject();

        var (engine, generic) = TaskEngineMethod(edgeMode);
        var taskResultType    = ParameterCodeModel.TaskResult(declaration).ParameterType;
        var wrapper           = ParameterCodeModel.GetTaskBeginAsParameter(declaration);
        var afterMethod       = $"{CodeGenFacts.ExitMethodPrefix}{namePascal}";
        var engineCall        = generic ? $"{engine}<{taskResultType}>" : engine;

        // Empfänger des Begin(…)-Aufrufs: bei [donotinject] der explizite Wrapper-Parameter (kein Feld
        // injiziert), sonst das injizierte _wfs._{task}-Feld.
        var beginReceiver = doNotInject
            ? CodeGenFacts.TaskBeginParameterName
            : $"{WfsFieldName}.{CodeGenFacts.FieldPrefix}{wrapper.ParameterName}";

        // Je Init-Knoten des Ziel-Tasks eine Begin{Node}-Überladung (analog V1-BeginWrapper).
        foreach (var init in declaration.Inits().WhereNotNull()) {

            var parameters = ParameterCodeModel.FromParameterSyntaxes(init.Syntax.CodeParamsDeclaration?.ParameterList)
                                               .ToList();

            var parameterList = String.Join(", ", parameters.Select(p => $"{p.ParameterType} {p.ParameterName}"));
            var argumentList  = String.Join(", ", parameters.Select(p => p.ParameterName));

            // [donotinject]: den expliziten Wrapper-Parameter voranstellen (V1-Port, s. remarks).
            if (doNotInject) {
                var wrapperParam = $"{wrapper.ParameterType} {CodeGenFacts.TaskBeginParameterName}";
                parameterList = parameterList.Length == 0
                    ? wrapperParam
                    : $"{wrapperParam}, {parameterList}";
            }

            var boundary = notImplemented
                ? $"throw new NotImplementedException(\"Task {task.Name} is specified as [notimplemented]\")"
                : $"{WfsFieldName}.{engineCall}(() => {beginReceiver}.{CodeGenFacts.BeginMethodPrefix}({argumentList}), {WfsFieldName}.{afterMethod})";

            yield return new TaskBeginPiece($"Begin{namePascal}", parameterList, boundary, notImplemented, wrapper.ParameterType);
        }
    }

    /// <summary>
    /// Baut den <c>{Choice}(…)</c>-Forward einer Quelle, die direkt auf eine Choice zeigt (§3.5): ruft die
    /// geteilte <c>{Choice}Logic(…)</c> auf und reicht deren fertiges Kommando (<c>Unwrap()</c>) durch. Die
    /// Argumente sind die Choice-Parameter (<c>choice X [params …]</c>); den Choice-Context konstruiert der
    /// target-getypte <c>new(_wfs)</c>. Rekursiv identisch für Choice→Choice (die Choice-Kette entfaltet der
    /// Codegen nicht, sondern forwardet eine Ebene tiefer).
    /// </summary>
    static Entry BuildChoiceForward(IChoiceNodeSymbol choice) {

        var namePascal = choice.Name.ToPascalcase();
        var logicName  = $"{namePascal}{CodeGenFacts.LogicMethodSuffix}";

        var parameters = ParameterCodeModel.FromParameterSyntaxes(choice.Syntax.CodeParamsDeclaration?.ParameterList)
                                           .ToList();

        var parameterList = String.Join(", ", parameters.Select(p => $"{p.ParameterType} {p.ParameterName}"));
        var argumentList  = String.Join(", ", parameters.Select(p => p.ParameterName));

        // Der Choice-Context ist der zweite Logic-Parameter → target-getyptes new(_wfs).
        var forwardArguments = argumentList.Length == 0
            ? $"new({WfsFieldName})"
            : $"{argumentList}, new({WfsFieldName})";

        return new Entry(
            SortOrderChoice,
            namePascal,
            new CallableMethodModel(
                signature    : $"{namePascal}({parameterList})",
                thunkBody    : $"{WfsFieldName}.{logicName}({forwardArguments}).Unwrap()",
                navChoiceName: choice.Name));
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

    const string ContinuationTypeSuffix = "Continuation";

    /// <summary>Namenssuffix aller Call-Context-Typen (Transition wie Choice): z.B. <c>Init1<b>CallContext</b></c>.</summary>
    public const string ContextTypeSuffix = "CallContext";

    // Reihenfolge-Kategorien (an V1s CallCodeModel.SortOrder angelehnt): Task, Gui, Choice, Exit, End, Cancel.
    const int SortOrderTask   = 1;
    const int SortOrderGui    = 2;
    const int SortOrderChoice = 3;
    const int SortOrderExit   = 4;
    const int SortOrderEnd    = 5;
    const int SortOrderCancel = Int32.MaxValue;

    readonly struct Entry {

        public Entry(int sortOrder, string name, CallableModel method) {
            SortOrder = sortOrder;
            Name      = name;
            Method    = method;
        }

        public int           SortOrder { get; }
        public string        Name      { get; }
        public CallableModel Method    { get; }

    }

    /// <summary>Die pro Init berechneten Bausteine einer <c>Begin{Node}</c>-Fortsetzung (siehe <see cref="BuildTaskBegins"/>).</summary>
    readonly struct TaskBeginPiece {

        public TaskBeginPiece(string name, string parameterList, string boundaryExpression, bool notImplemented, string interfaceFqn) {
            Name               = name;
            ParameterList      = parameterList;
            BoundaryExpression = boundaryExpression;
            NotImplemented     = notImplemented;
            InterfaceFqn       = interfaceFqn;
        }

        /// <summary>Der Methodenname <c>Begin{Node}</c>.</summary>
        public string Name               { get; }
        /// <summary>Die Parameterliste des Init-Knotens (<c>{Typ} {Name}</c>, kommasepariert).</summary>
        public string ParameterList      { get; }
        /// <summary>Das Framework-Kommando (Boundary) bzw. der <c>throw</c> bei <c>[notimplemented]</c>.</summary>
        public string BoundaryExpression { get; }
        /// <summary>Ob der Ziel-Task <c>[notimplemented]</c> ist (<see cref="BoundaryExpression"/> ist dann ein <c>throw</c>).</summary>
        public bool   NotImplemented     { get; }
        /// <summary>Der voll qualifizierte <c>IBegin{Task}WFS</c>-Interface-Name des Ziel-Tasks (Inhalt der <c>NavInitCall</c>-Annotation).</summary>
        public string InterfaceFqn       { get; }

    }

}

/// <summary>Gemeinsame Basis der Callables eines <see cref="CallContextCodeModel"/> (siehe konkrete Ableitungen).</summary>
abstract class CallableModel {
}

/// <summary>
/// Eine schlichte Callable-Methode eines <see cref="CallContextCodeModel"/>: ihre Signatur und der
/// Ausdruck, den der deferred <c>Func&lt;…&gt;</c>-Thunk beim <c>Unwrap()</c>-Aufruf auswertet. Der
/// Emitter schreibt daraus uniform <c>public Result {Signature} =&gt; new(() =&gt; {ThunkBody});</c>.
/// Verwendet für plain <c>Show{Node}</c>, <c>Begin{Node}</c>, <c>Exit</c>, <c>End</c>, <c>Cancel</c>
/// sowie die <c>Begin{Task}(…)</c>-Fortsetzungen eines <see cref="ShowContinuationCallableModel"/>.
/// </summary>
sealed class CallableMethodModel: CallableModel {

    public CallableMethodModel(string signature, string thunkBody, string? navInitCallInterface = null, string? navChoiceName = null) {
        Signature            = signature;
        ThunkBody            = thunkBody;
        NavInitCallInterface = navInitCallInterface;
        NavChoiceName        = navChoiceName;
    }

    public string Signature { get; }
    public string ThunkBody { get; }

    /// <summary>
    /// Ist die Callable ein <c>Begin{Node}</c>-Sub-Task-Aufruf, trägt sie hier den voll qualifizierten
    /// <c>IBegin{Task}WFS</c>-Interface-Namen — der Emitter schreibt daraus die <c>NavInitCall</c>-Annotation
    /// (C#→BeginLogic-Navigation, gelesen vom <c>AnnotationReader</c> am Aufrufort <c>ctx.Begin{Node}(…)</c>).
    /// <c>null</c> für alle übrigen Callables (<c>Show</c>/<c>Exit</c>/<c>End</c>/<c>Cancel</c>/Choice-Forward).
    /// </summary>
    public string? NavInitCallInterface { get; }

    /// <summary>
    /// Ist die Callable ein <c>{Choice}(…)</c>-Forward, trägt sie hier den Choice-Knotennamen — der Emitter
    /// schreibt daraus die <c>NavChoiceCall</c>-Annotation (C#→Nav-Navigation, gelesen vom
    /// <c>AnnotationReader</c> am Aufrufort <c>next.{Choice}(…)</c>, führt zum Choice-Knoten im <c>.nav</c>).
    /// <c>null</c> für alle übrigen Callables.
    /// </summary>
    public string? NavChoiceName { get; }

}

/// <summary>
/// Die Continuation-Aufruffläche einer View-Kante (<c>… o-^ Task</c> / <c>… --^ Task</c>): die
/// <c>Show{Node}</c>-Methode liefert statt eines <c>Result</c> einen geschachtelten
/// <c>Show{Node}Continuation</c>-Typ. Dieser trägt je Continuation-Kante eine
/// <c>Begin{Task}(…)</c>-Fortsetzung (<see cref="Begins"/>, baut <c>GotoGUI(to).Concat(…)</c>) und —
/// sofern eine plain-Schwesterkante zur selben View existiert (<see cref="PlainThunkBody"/> ≠ null) —
/// einen impliziten <c>Result</c>-Operator (§3.4/§3.6).
/// </summary>
sealed class ShowContinuationCallableModel: CallableModel {

    public ShowContinuationCallableModel(string entryMethodSignature,
                                         string continuationTypeName,
                                         string toParameterType,
                                         string? plainThunkBody,
                                         ImmutableList<CallableMethodModel> begins) {
        EntryMethodSignature = entryMethodSignature;
        ContinuationTypeName = continuationTypeName;
        ToParameterType      = toParameterType;
        PlainThunkBody       = plainThunkBody;
        Begins               = begins;
    }

    /// <summary>Signatur der Einstiegsmethode auf dem Context (z.B. <c>ShowHome(HomeTO to)</c>).</summary>
    public string EntryMethodSignature { get; }

    /// <summary>Name des geschachtelten Continuation-Typs (z.B. <c>ShowHomeContinuation</c>).</summary>
    public string ContinuationTypeName { get; }

    /// <summary>Typ des <c>ViewTO</c>-Felds/-Parameters (z.B. <c>HomeTO</c>).</summary>
    public string ToParameterType { get; }

    /// <summary>
    /// Der Thunk-Rumpf des impliziten <c>Result</c>-Operators (plain-Kante, referenziert die Felder über
    /// den Operanden <c>v</c>) — <c>null</c>, wenn keine plain-Schwesterkante existiert (erzwungene
    /// Continuation, kein impliziter Operator).
    /// </summary>
    public string? PlainThunkBody { get; }

    /// <summary>Die <c>Begin{Task}(…)</c>-Fortsetzungen (je Continuation-Kante bzw. Ziel-Init eine).</summary>
    public ImmutableList<CallableMethodModel> Begins { get; }

}
