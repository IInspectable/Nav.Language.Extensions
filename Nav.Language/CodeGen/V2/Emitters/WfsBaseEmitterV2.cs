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

    public static string Emit(WfsBaseCodeModelV2 model, CodeGeneratorContext context) {

        var cb = new CodeBuilder();

        EmitterCommon.WriteFileHeader(cb, context);
        EmitterCommon.WriteUsingDirectives(cb, model.UsingNamespaces);

        cb.Write($"""

                  namespace {model.WflNamespace}
                  """);
        cb.Write(" ");
        using (cb.Block()) {

            WriteBaseClass(cb, model);

            cb.WriteLine();
            cb.WriteLine();

            WriteWfsClass(cb, model);
        }

        return cb.ToString();
    }

    // -- {Task}WFSBase: die abstrakte Maschinerie-Basisklasse -----------------------------------------

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

            foreach (var transition in model.InitTransitions) {
                WriteTransition(cb, model, transition);
            }

            foreach (var transition in model.ExitTransitions) {
                WriteTransition(cb, model, transition);
            }

            foreach (var transition in model.TriggerTransitions) {
                WriteTransition(cb, model, transition);
            }
        }
    }

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

    // -- Der Call-Context einer Quelle ----------------------------------------------------------------

    static void WriteCallContext(CodeBuilder cb, WfsBaseCodeModelV2 model, CallContextCodeModel context) {

        cb.Write($"protected sealed class {context.ContextTypeName} ");
        using (cb.Block()) {

            cb.WriteLine();

            cb.WriteLine($"readonly {model.WfsBaseTypeName} {CallContextCodeModel.WfsFieldName};");
            cb.WriteLine($"internal {context.ContextTypeName}({model.WfsBaseTypeName} wfs) => {CallContextCodeModel.WfsFieldName} = wfs;");
            cb.WriteLine();

            WriteResultStruct(cb, context);
            cb.WriteLine();

            foreach (var method in context.Methods) {
                switch (method) {
                    case CallableMethodModel simple:
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

    static void WriteShowContinuation(CodeBuilder cb, WfsBaseCodeModelV2 model, ShowContinuationCallableModel continuation) {

        // Einstieg auf dem Context: Show{Node} liefert den Continuation-Typ (kein Result).
        cb.WriteLine($"public {continuation.ContinuationTypeName} {continuation.EntryMethodSignature} => new({CallContextCodeModel.WfsFieldName}, {CallContextCodeModel.ToParameterName});");

        cb.Write($"public sealed class {continuation.ContinuationTypeName} ");
        using (cb.Block()) {

            cb.WriteLine($"readonly {model.WfsBaseTypeName} {CallContextCodeModel.WfsFieldName};");
            cb.WriteLine($"readonly {continuation.ToParameterType} {CallContextCodeModel.ToFieldName};");
            cb.WriteLine($"internal {continuation.ContinuationTypeName}({model.WfsBaseTypeName} wfs, {continuation.ToParameterType} {CallContextCodeModel.ToParameterName}) {{ {CallContextCodeModel.WfsFieldName} = wfs; {CallContextCodeModel.ToFieldName} = {CallContextCodeModel.ToParameterName}; }}");
            cb.WriteLine();

            // plain-Schwesterkante vorhanden → impliziter Result-Operator (§3.6).
            if (continuation.PlainThunkBody != null) {
                cb.WriteLine($"public static implicit operator Result({continuation.ContinuationTypeName} v) => new(() => {continuation.PlainThunkBody});");
            }

            foreach (var begin in continuation.Begins) {
                cb.WriteLine($"public Result {begin.Signature} => new(() => {begin.ThunkBody});");
            }
        }

        cb.WriteLine();
    }

    static void WriteResultStruct(CodeBuilder cb, CallContextCodeModel context) {

        var commandType = context.CommandType;

        // Opaker Ergebnistyp: nur dieser Context kann ihn erzeugen; das Kommando wird deferred im Thunk
        // gebaut (§3.2). Unwrap() ist internal — die Maschinerie in {Task}WFSBase ist Container von Result
        // und erreicht dessen private Member NICHT (§3.2).
        cb.Write("public readonly struct Result ");
        using (cb.Block()) {
            cb.WriteLine($"readonly System.Func<{commandType}> _command;");
            cb.WriteLine($"internal Result(System.Func<{commandType}> command) => _command = command;");
            cb.WriteLine();
            cb.WriteLine($"internal {commandType} Unwrap()");
            using (cb.Indent()) {
                cb.WriteLine("=> _command is null");
                using (cb.Indent()) {
                    cb.WriteLine("? throw new InvalidOperationException(");
                    using (cb.Indent()) {
                        cb.WriteLine("\"A Logic method returned default(Result); every code path must return a navigation result via the call context.\")");
                    }

                    cb.WriteLine(": _command();");
                }
            }
        }

        cb.WriteLine();
    }

    // -- {Task}WFS: die partielle Implementierungsklasse (V1-deckungsgleich) --------------------------

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
                         .Concat(new[] { $"{transition.Context!.ContextTypeName} callContext" });
    }

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
