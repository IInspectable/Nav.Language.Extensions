#region Using Directives

using System.Collections.Generic;
using System.Linq;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// CodeBuilder-Emitter der <c>{Task}WFSBase</c>-Familie der <b>Generation 2</b> (CallContext). Erzeugt
/// in einer Datei die abstrakte Maschinerie-Basisklasse <c>{Task}WFSBase</c> und die partielle
/// Implementierungsklasse <c>{Task}WFS</c> (Felder/Konstruktoren — der Rumpf der Logic-Methoden lebt
/// in der OneShot-Datei).
/// </summary>
/// <remarks>
/// Der Kernunterschied zu <see cref="WfsBaseEmitter"/> (V1): Jede Transition kollabiert auf einen
/// nackten <c>…Logic(args, new {Context}(this)).Unwrap()</c>-Aufruf (§3.3) und trägt einen
/// geschachtelten <c>{Context}CallContext</c> mit opakem <c>Result</c>; den V1-<c>switch(body)</c>
/// samt Begin-Wrapper-Hilfsmethoden und <c>TaskResult</c>-Helfer gibt es nicht mehr — die
/// Kommando-Konstruktion sitzt deferred in den Context-Methoden. Die
/// <c>{Task}WFS</c>-Partial-Klasse (Felder/Konstruktoren) ist dagegen V1-deckungsgleich.
/// </remarks>
static class WfsBaseEmitterV2 {

    /// <summary>Name des zentralen <c>Result.Unwrap()</c>-Guards (einmal je <c>{Task}WFSBase</c>).</summary>
    const string UnwrapHelperName = "UnwrapOrThrow";

    /// <summary>
    /// Rendert die <c>{Task}WFSBase</c>-Datei aus <paramref name="model"/>: den <c>&lt;auto-generated&gt;</c>-Kopf,
    /// die <c>using</c>s und im Task-Namespace die abstrakte Basisklasse <c>{Task}WFSBase</c> gefolgt von der
    /// partiellen Implementierungsklasse <c>{Task}WFS</c>.
    /// </summary>
    public static string Emit(WfsBaseCodeModelV2 model, CodeGeneratorContext context) {

        var cb = new CodeBuilder();

        EmitterCommon.WriteFileHeader(cb, context);
        EmitterCommon.WriteUsingDirectives(cb, model.UsingNamespaces);

        EmitterCommon.WriteNamespace(cb, model.WflNamespace);

        WriteBaseClass(cb, model);

        cb.WriteLine();
        cb.WriteLine();

        WriteWfsClass(cb, model);

        return cb.ToString();
    }

    // -- {Task}WFSBase: die abstrakte Maschinerie-Basisklasse -----------------------------------------

    /// <summary>
    /// Schreibt die abstrakte Basisklasse <c>{Task}WFSBase</c>: Begin-Wrapper-Felder, Konstruktoren, die
    /// <c>BeforeTriggerLogic</c>-Überladungen, den einmaligen <see cref="WriteUnwrapHelper"/>-Guard sowie je
    /// Init-/Exit-/Trigger-Transition und Choice deren Maschinerie/Logic/Call-Context.
    /// </summary>
    static void WriteBaseClass(CodeBuilder cb, WfsBaseCodeModelV2 model) {

        EmitterCommon.WriteTaskAnnotation(cb, model.RelativeSyntaxFileName, model.Task.TaskName);
        cb.Write($"public abstract partial class {model.WfsBaseTypeName}: {model.WfsBaseBaseTypeName} ");

        using (cb.Block()) {

            cb.WriteLine();

            if (model.TaskBegins.Count > 0) {
                foreach (var taskBegin in model.TaskBegins) {
                    cb.WriteLine($"readonly {taskBegin.ParameterType} {CodeGenFacts.FieldPrefix}{taskBegin.ParameterName} = default!;");
                }

                cb.WriteLine();
            }

            cb.WriteLine($"public {model.WfsBaseTypeName}({CodeGenFacts.NavigationEngineIwflNamespace}.IClientSideWFS clientSideWFS) {{}}");
            cb.WriteLine();

            WriteBaseConstructor(cb, model);
            cb.WriteLine();

            foreach (var viewParameter in model.ViewParameters) {
                cb.WriteLine($"protected virtual {viewParameter.ParameterType} {CodeGenFacts.BeforeTriggerLogicMethodName}({viewParameter.ParameterType} {viewParameter.ParameterName}) => {viewParameter.ParameterName};");
                cb.WriteLine();
            }

            // Der Null-/default-Guard aller Result.Unwrap() sitzt einmalig hier (nur nötig, wenn
            // überhaupt ein Call-Context — und damit ein Result — emittiert wird).
            if (EmitsAnyContext(model)) {
                WriteUnwrapHelper(cb, model);
            }

            foreach (var transition in model.InitTransitions) {
                WriteTransition(cb, model, transition);
            }

            foreach (var transition in model.ExitTransitions) {
                WriteTransition(cb, model, transition);
            }

            foreach (var transition in model.TriggerTransitions) {
                WriteTransition(cb, model, transition);
            }

            // Choices als eigene Bausteine (§3.5): je Choice die abstrakte {Choice}Logic + ihr Call-Context —
            // ohne öffentliche Maschinerie-Methode (Choices werden nur über {Choice}(…)-Forwards erreicht).
            foreach (var choice in model.Choices) {
                WriteChoice(cb, model, choice);
            }
        }
    }

    /// <summary>Schreibt den Konstruktor, der die Begin-Wrapper der Sub-Tasks entgegennimmt und in die <c>_x</c>-Felder legt.</summary>
    static void WriteBaseConstructor(CodeBuilder cb, WfsBaseCodeModelV2 model) {

        cb.Write($"public {model.WfsBaseTypeName}(");
        WriteParameterList(cb, model.TaskBegins);
        cb.Write(") ");

        using (cb.Block()) {
            foreach (var taskBegin in model.TaskBegins) {
                cb.WriteLine($"{CodeGenFacts.FieldPrefix}{taskBegin.ParameterName} = {taskBegin.ParameterName};");
            }
        }

        cb.WriteLine();
    }

    // -- Eine Transition: Maschinerie + abstrakte Logic + Call-Context --------------------------------

    /// <summary>
    /// Schreibt eine Transition: bei <see cref="TransitionCallContextCodeModel.GenerateAbstractMachinery"/> nur
    /// die abstrakte Maschinerie-Methode; sonst die (bei Trigger um den <c>BeforeTriggerLogic</c>-Vorlauf ergänzte)
    /// Maschinerie-Methode, die auf <c>…Logic(args, new {Context}(this)).Unwrap()</c> kollabiert (§3.3), gefolgt
    /// von der abstrakten <c>…Logic</c>-Gegenstelle und dem Call-Context der Quelle.
    /// </summary>
    static void WriteTransition(CodeBuilder cb, WfsBaseCodeModelV2 model, TransitionCallContextCodeModel transition) {

        WriteTransitionAnnotation(cb, transition);

        // [abstract]-Quelle: nur die Maschinerie-Methode selbst, abstrakt und vom Nutzer implementiert.
        if (transition.GenerateAbstractMachinery) {
            cb.Write($"{transition.AccessModifier} abstract {transition.ReturnType} {transition.MachineryName}(");
            WriteParameterList(cb, transition.Parameters);
            cb.WriteLine(");");
            cb.WriteLine();
            return;
        }

        var callArguments = LogicCallArguments(transition);

        if (transition.IsTrigger) {
            // Trigger: der BeforeTriggerLogic-Vorlauf bleibt, danach der nackte Unwrap()-Aufruf.
            var viewParamName = transition.Parameters[0].ParameterName;

            cb.Write($"{transition.AccessModifier} virtual {transition.ReturnType} {transition.MachineryName}(");
            WriteParameterList(cb, transition.Parameters);
            cb.Write(") ");
            using (cb.Block()) {
                cb.WriteLine($"{viewParamName} = {CodeGenFacts.BeforeTriggerLogicMethodName}({viewParamName});");
                cb.WriteLine($"return {transition.LogicName}({callArguments}).Unwrap();");
            }

            cb.WriteLine();
        } else {
            // Init/Exit: expression-bodied, kein Vorlauf.
            cb.Write($"{transition.AccessModifier} virtual {transition.ReturnType} {transition.MachineryName}(");
            WriteParameterList(cb, transition.Parameters);
            cb.WriteLine(")");

            using (cb.Indent()) {
                cb.WriteLine($"=> {transition.LogicName}({callArguments}).Unwrap();");
            }
        }

        cb.WriteLine();

        // Abstrakte Logic-Gegenstelle.
        WriteTransitionAnnotation(cb, transition);
        cb.Write($"protected abstract {transition.Context!.ContextTypeName}.Result {transition.LogicName}(");
        WriteAlignedDecls(cb, LogicSignatureDeclarations(transition));
        cb.WriteLine(");");
        cb.WriteLine();

        // Der Call-Context der Quelle.
        WriteCallContext(cb, model, transition.Context);
        cb.WriteLine();
    }

    // -- Eine Choice: abstrakte Logic + Call-Context (keine Maschinerie-Methode) ----------------------

    /// <summary>
    /// Schreibt eine Choice (§3.5): nur die abstrakte <c>{Choice}Logic</c> (mit <c>&lt;NavChoice&gt;</c>-Annotation)
    /// und ihren Call-Context — keine öffentliche Maschinerie-Methode, da eine Choice nur über die
    /// <c>{Choice}(…)</c>-Forwards ihrer Quellen erreicht wird.
    /// </summary>
    static void WriteChoice(CodeBuilder cb, WfsBaseCodeModelV2 model, ChoiceCallContextCodeModel choice) {

        // Die Entscheidung liegt EINMAL beim Nutzer — die Quellen forwarden nur (§3.5). Anders als eine
        // Transition hat eine Choice keine öffentliche Weiche; es gibt daher nur die abstrakte Logic.
        // Die <NavChoice>-Annotation trägt den C#→Nav-Rückweg (Intra-Text-GoTo auf {Choice}Logic).
        EmitterCommon.WriteNavChoiceAnnotation(cb, choice.ChoiceName);
        cb.Write($"protected abstract {choice.Context.ContextTypeName}.Result {choice.LogicName}(");
        WriteAlignedDecls(cb, ChoiceLogicDeclarations(choice));
        cb.WriteLine(");");
        cb.WriteLine();

        WriteCallContext(cb, model, choice.Context);
        cb.WriteLine();
    }

    // -- Der Call-Context einer Quelle ----------------------------------------------------------------

    /// <summary>
    /// Schreibt den geschachtelten <c>protected sealed class {Context}CallContext</c>: der <c>_wfs</c>-Rückverweis
    /// als Primärkonstruktor-Parameter, den <see cref="WriteResultStruct"/>-Ergebnistyp und je Callable die Aufrufmethode — schlichte
    /// <c>public Result … => new(() =&gt; {Thunk});</c> (mit ggf. <c>NavInitCall</c>-/<c>NavChoiceCall</c>-Annotation)
    /// bzw. eine Continuation-Aufruffläche (<see cref="WriteShowContinuation"/>).
    /// </summary>
    static void WriteCallContext(CodeBuilder cb, WfsBaseCodeModelV2 model, CallContextCodeModel context) {

        // Der Rückverweis auf die tragende {Task}WFSBase ist ein Primärkonstruktor-Parameter (eingefangen
        // als privates Feld _wfs); eine eigene Feld-/Konstruktor-Zeile entfällt.
        cb.Write($"protected sealed class {context.ContextTypeName}({model.WfsBaseTypeName} {CallContextCodeModel.WfsFieldName}) ");
        using (cb.Block()) {

            cb.WriteLine();

            WriteResultStruct(cb, context);
            cb.WriteLine();

            foreach (var method in context.Methods) {
                switch (method) {
                    case CallableMethodModel simple:
                        if (simple.NavInitCallInterface != null) {
                            EmitterCommon.WriteInitCallAnnotation(cb, simple.NavInitCallInterface);
                        }

                        if (simple.NavChoiceName != null) {
                            EmitterCommon.WriteNavChoiceCallAnnotation(cb, simple.NavChoiceName);
                        }

                        cb.WriteLine($"public Result {simple.Signature} => new(() => {simple.ThunkBody});");
                        break;
                    case ShowContinuationCallableModel continuation:
                        WriteShowContinuation(cb, model, continuation);
                        break;
                }
            }
        }

        cb.WriteLine();
    }

    // -- Die Continuation-Aufruffläche einer View-Kante (… o-^ Task / … --^ Task) ----------------------

    /// <summary>
    /// Schreibt die Continuation-Aufruffläche: die <c>Show{Node}</c>-Einstiegsmethode (liefert den
    /// geschachtelten <c>Show{Node}Continuation</c>-Typ statt eines <c>Result</c>) und diesen Typ selbst — mit
    /// seinen <c>_wfs</c>/<c>_to</c>-Feldern, dem impliziten <c>Result</c>-Operator (nur bei plain-Schwesterkante,
    /// §3.6) und je <c>Begin{Task}</c>-Fortsetzung einer <c>Result</c>-Methode.
    /// </summary>
    static void WriteShowContinuation(CodeBuilder cb, WfsBaseCodeModelV2 model, ShowContinuationCallableModel continuation) {

        // Einstieg auf dem Context: Show{Node} liefert den Continuation-Typ (kein Result).
        cb.WriteLine($"public {continuation.ContinuationTypeName} {continuation.EntryMethodSignature} => new({CallContextCodeModel.WfsFieldName}, {CallContextCodeModel.ToParameterName});");

        // Explizite Felder (kein Primärkonstruktor): der statische implizite Result-Operator liest die
        // Felder qualifiziert über eine andere Instanz (v._wfs/v._to) — ein per Primärkonstruktor
        // eingefangener Parameter wäre so nicht erreichbar (kein benannter Member, nur unqualifiziert
        // im Instanz-Rumpf zugreifbar). Anders als CallContext/Result braucht die Continuation die
        // Felder daher als echte Member.
        cb.Write($"public sealed class {continuation.ContinuationTypeName} ");
        using (cb.Block()) {

            cb.WriteLine($$"""
                           readonly {{model.WfsBaseTypeName}} {{CallContextCodeModel.WfsFieldName}};
                           readonly {{continuation.ToParameterType}} {{CallContextCodeModel.ToFieldName}};
                           internal {{continuation.ContinuationTypeName}}({{model.WfsBaseTypeName}} wfs, {{continuation.ToParameterType}} {{CallContextCodeModel.ToParameterName}}) { {{CallContextCodeModel.WfsFieldName}} = wfs; {{CallContextCodeModel.ToFieldName}} = {{CallContextCodeModel.ToParameterName}}; }
                           """);
            cb.WriteLine();

            // plain-Schwesterkante vorhanden → impliziter Result-Operator (§3.6).
            if (continuation.PlainThunkBody != null) {
                cb.WriteLine($"public static implicit operator Result({continuation.ContinuationTypeName} v) => new(() => {continuation.PlainThunkBody});");
            }

            foreach (var begin in continuation.Begins) {
                if (begin.NavInitCallInterface != null) {
                    EmitterCommon.WriteInitCallAnnotation(cb, begin.NavInitCallInterface);
                }

                cb.WriteLine($"public Result {begin.Signature} => new(() => {begin.ThunkBody});");
            }
        }

        cb.WriteLine();
    }

    /// <summary>
    /// Schreibt den opaken <c>public readonly struct Result</c> des Contexts (§3.2): trägt den deferred
    /// <c>Func&lt;{CommandType}&gt;</c>-Thunk, ein <c>internal</c> <c>Unwrap()</c>, das über den geteilten
    /// <see cref="UnwrapHelperName"/>-Guard entfaltet und dabei per <c>nameof</c> das schuldige Logic-Override benennt.
    /// </summary>
    static void WriteResultStruct(CodeBuilder cb, CallContextCodeModel context) {

        var commandType = context.CommandType;

        // Opaker Ergebnistyp: nur dieser Context kann ihn erzeugen; das Kommando wird deferred im Thunk
        // gebaut (§3.2). Unwrap() ist internal — die Maschinerie in {Task}WFSBase ist Container von Result
        // und erreicht dessen private Member NICHT (§3.2). Der Null-/default-Guard sitzt einmalig in
        // {Task}WFSBase.UnwrapOrThrow (kein pro-Context-Duplikat); nameof benennt das Logic-Override,
        // das beim Wurf nicht mehr auf dem Stack steht.
        cb.Write($"public readonly struct Result(System.Func<{commandType}> _command) ");
        using (cb.Block()) {
            cb.Write($"""
                      internal {commandType} Unwrap() => {UnwrapHelperName}(_command, nameof({context.LogicMethodName}));
                      """);
        }

        cb.WriteLine();
    }

    /// <summary>
    /// Der zentrale Null-/<c>default</c>-Guard aller <c>Result.Unwrap()</c> dieser <c>{Task}WFSBase</c>
    /// (§3.2): feuert den deferred Kommando-Thunk und wirft nur beim expliziten <c>return default;</c>
    /// (Func == null). Einmal je Basisklasse — die genesteten <c>Result</c>-Structs leiten hierher weiter,
    /// statt Guard und Meldung pro Context zu duplizieren. Die Meldung benennt Task und Logic-Override
    /// (via <c>nameof</c> aus <c>Unwrap()</c>): das Override ist beim Wurf bereits returned und steht
    /// nicht mehr auf dem Stack — der Stacktrace allein zeigt nur die Maschinerie-Methode bzw. beim
    /// Choice-Forward einen Compiler-generierten Lambda-Frame. Die Verkettung liegt im throw-Zweig
    /// (keine Allokation im Erfolgspfad).
    /// </summary>
    static void WriteUnwrapHelper(CodeBuilder cb, WfsBaseCodeModelV2 model) {
        cb.WriteLine($"""
                  static TCommand {UnwrapHelperName}<TCommand>(System.Func<TCommand> command, string logicMethodName)
                      => command is null
                          ? throw new InvalidOperationException(
                              logicMethodName + " of task '{model.Task.TaskName}' returned default(Result); every code path must return a navigation result via the call context.")
                          : command();

                  """);
    }

    /// <summary>Wird für diese WFSBase überhaupt ein Call-Context (und damit ein <c>Result</c>) emittiert?</summary>
    static bool EmitsAnyContext(WfsBaseCodeModelV2 model) {
        return model.Choices.Count > 0 ||
               model.InitTransitions
                    .Concat(model.ExitTransitions)
                    .Concat(model.TriggerTransitions)
                    .Any(t => !t.GenerateAbstractMachinery);
    }

    // -- {Task}WFS: die partielle Implementierungsklasse (V1-deckungsgleich) --------------------------

    /// <summary>
    /// Schreibt die partielle Implementierungsklasse <c>{Task}WFS</c> (V1-deckungsgleich): sie leitet von
    /// <c>{Task}WFSBase</c> ab und implementiert <c>I{Task}WFS</c>/<c>IBegin{Task}WFS</c>; trägt die
    /// Task-Parameter-Felder und die beiden Konstruktoren.
    /// </summary>
    static void WriteWfsClass(CodeBuilder cb, WfsBaseCodeModelV2 model) {

        var iwfsName      = model.Task.IWfsTypeName;
        var iBeginWfsName = $"{CodeGenInvariants.BeginInterfacePrefix}{model.Task.TaskNamePascalcase}{CodeGenInvariants.InterfaceSuffix}";

        EmitterCommon.WriteTaskAnnotation(cb, model.RelativeSyntaxFileName, model.Task.TaskName);
        cb.Write($"public partial class {model.WfsTypeName}: {model.WfsBaseTypeName}, {iwfsName}, {iBeginWfsName} ");

        using (cb.Block()) {

            cb.WriteLine();

            if (model.TaskParameter.Count > 0) {
                foreach (var taskParameter in model.TaskParameter) {
                    cb.WriteLine($"readonly {taskParameter.ParameterType} {CodeGenFacts.FieldPrefix}{taskParameter.ParameterName} = default!;");
                }

                cb.WriteLine();
            }

            cb.WriteLine($"public {model.WfsTypeName}({CodeGenFacts.NavigationEngineIwflNamespace}.IClientSideWFS clientSideWFS): base(clientSideWFS) {{}}");
            cb.WriteLine();

            cb.Write($"public {model.WfsTypeName}(");
            WriteParameterList(cb, model.TaskBegins.Concat(model.TaskParameter));
            cb.WriteLine(")");

            cb.PushIndent();
            cb.Write(":base(");
            WriteExpressionListMultiline(cb, model.TaskBegins);
            cb.Write(")");
            cb.PopIndent();

            cb.Write(" ");
            using (cb.Block()) {
                foreach (var taskParameter in model.TaskParameter) {
                    cb.WriteLine($"{CodeGenFacts.FieldPrefix}{taskParameter.ParameterName} = {taskParameter.ParameterName};");
                }
            }

            cb.WriteLine();
        }
    }

    // -- Annotationen ---------------------------------------------------------------------------------

    /// <summary>Schreibt die Nav-Annotation der Transition je nach <see cref="TransitionCallContextCodeModel.AnnotationKind"/>.</summary>
    static void WriteTransitionAnnotation(CodeBuilder cb, TransitionCallContextCodeModel transition) {
        switch (transition.AnnotationKind) {
            case TransitionAnnotationKind.Init:
                EmitterCommon.WriteNavInitAnnotation(cb, transition.AnnotationName);
                break;
            case TransitionAnnotationKind.Exit:
                EmitterCommon.WriteNavExitAnnotation(cb, transition.AnnotationName);
                break;
            case TransitionAnnotationKind.Trigger:
                EmitterCommon.WriteTriggerAnnotation(cb, transition.AnnotationName);
                break;
        }
    }

    // -- Parameter-/Ausdruckslisten -------------------------------------------------------------------

    /// <summary>Die Argumente des <c>…Logic(…)</c>-Aufrufs: die Transitions-Parameter plus den neuen Context.</summary>
    static string LogicCallArguments(TransitionCallContextCodeModel transition) {
        var arguments = transition.Parameters.Select(p => p.ParameterName)
                                  .Concat(new[] { $"new {transition.Context!.ContextTypeName}(this)" });
        return string.Join(", ", arguments);
    }

    /// <summary>Die Parameter-Deklarationen der abstrakten <c>…Logic(…)</c>-Signatur (inkl. Context-Parameter).</summary>
    static IEnumerable<string> LogicSignatureDeclarations(TransitionCallContextCodeModel transition) {
        return transition.Parameters.Select(p => $"{p.ParameterType} {p.ParameterName}")
                         .Concat(new[] { $"{transition.Context!.ContextTypeName} {CallContextCodeModel.ContextParameterName}" });
    }

    /// <summary>Die Parameter-Deklarationen der abstrakten <c>{Choice}Logic(…)</c>-Signatur (inkl. Context-Parameter).</summary>
    static IEnumerable<string> ChoiceLogicDeclarations(ChoiceCallContextCodeModel choice) {
        return choice.Parameters.Select(p => $"{p.ParameterType} {p.ParameterName}")
                     .Concat(new[] { $"{choice.Context.ContextTypeName} {CallContextCodeModel.ContextParameterName}" });
    }

    /// <summary>Umbrochene, an der öffnenden Klammer ausgerichtete Deklarationsliste (vorformatierte <c>{Typ} {Name}</c>-Strings).</summary>
    static void WriteAlignedDecls(CodeBuilder cb, IEnumerable<string> declarations) {
        cb.WriteAlignedJoin(declarations, d => cb.Write(d), separator: $",{cb.NewLine}");
    }

    /// <summary>Umbrochene, an der öffnenden Klammer ausgerichtete Parameterliste (<c>{Typ} {Name}</c>).</summary>
    static void WriteParameterList(CodeBuilder cb, IEnumerable<ParameterCodeModel> parameters) {
        cb.WriteAlignedJoin(parameters, p => cb.Write($"{p.ParameterType} {p.ParameterName}"), separator: $",{cb.NewLine}");
    }

    /// <summary>Umbrochene, ausgerichtete Argumentliste (nur die Namen) — für den <c>:base(…)</c>-Aufruf.</summary>
    static void WriteExpressionListMultiline(CodeBuilder cb, IEnumerable<ParameterCodeModel> parameters) {
        cb.WriteAlignedJoin(parameters, p => cb.Write(p.ParameterName), separator: $",{cb.NewLine}");
    }

}
